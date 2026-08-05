using System.Text;
using LiteDB;
using Novolis.Storage.LiteDb;

namespace Novolis.Tools.LiteDb;

/// <summary>Open flags for <see cref="LiteDbSession"/>.</summary>
/// <param name="DataSource">File path, <c>:memory:</c>, or connection string (caller-resolved path preferred).</param>
/// <param name="Password">Optional AES password.</param>
/// <param name="ReadOnly">Open existing file read-only.</param>
public sealed record LiteDbOpenSettings(string DataSource, string? Password = null, bool ReadOnly = false);

/// <summary>Tabular projection of a LiteDB shell command.</summary>
public sealed class LiteDbQueryResult
{
    /// <summary>Column names in display order.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>Row values aligned to <see cref="Columns"/>.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    /// <summary>Hint for non-result commands.</summary>
    public int? RecordsAffected { get; init; }

    /// <summary>Formats a fixed-width text table.</summary>
    public string ToTable()
    {
        if (Columns.Count == 0)
        {
            return RecordsAffected is int n ? $"{n} document(s) affected" : string.Empty;
        }

        var widths = Columns.Select((c, i) =>
            Math.Max(c.Length, Rows.Count == 0 ? 0 : Rows.Max(r => r[i].Length))).ToArray();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join('|', Columns.Select((c, i) => " " + c.PadRight(widths[i]) + " ")));
        sb.AppendLine(string.Join('+', widths.Select(w => new string('-', w + 2))));
        foreach (var row in Rows)
        {
            sb.AppendLine(string.Join('|', row.Select((c, i) => " " + c.PadRight(widths[i]) + " ")));
        }

        sb.Append($"{Rows.Count} document(s)");
        return sb.ToString();
    }

    /// <summary>Formats as CSV.</summary>
    public string ToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', Columns.Select(CsvEscape)));
        foreach (var row in Rows)
        {
            sb.AppendLine(string.Join(',', row.Select(CsvEscape)));
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>Formats each row as a JSON object (one document per line).</summary>
    public string ToJsonLines()
    {
        if (Columns.Count == 0)
        {
            return RecordsAffected is int n ? $"{{\"affected\":{n}}}" : string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var row in Rows)
        {
            var doc = new BsonDocument();
            for (var i = 0; i < Columns.Count; i++)
            {
                doc[Columns[i]] = row[i] == "NULL" ? BsonValue.Null : new BsonValue(row[i]);
            }

            sb.AppendLine(JsonSerializer.Serialize(doc));
        }

        return sb.ToString().TrimEnd();
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}

/// <summary>Opened LiteDB database with CLI-oriented helpers.</summary>
public sealed class LiteDbSession : IDisposable
{
    private readonly ILiteDatabase _database;
    private readonly bool _ownsDatabase;
    private readonly bool _readOnly;
    private bool _disposed;

    private LiteDbSession(ILiteDatabase database, bool ownsDatabase, bool readOnly, string dataSource)
    {
        _database = database;
        _ownsDatabase = ownsDatabase;
        _readOnly = readOnly;
        DataSource = dataSource;
    }

    /// <summary>True when opened read-only.</summary>
    public bool IsReadOnly => _readOnly;

    /// <summary>Resolved data source label.</summary>
    public string DataSource { get; }

    /// <summary>Opens a database file or <c>:memory:</c>.</summary>
    public static LiteDbSession Open(string dataSource, string? password = null, bool readOnly = false) =>
        Open(new LiteDbOpenSettings(dataSource, password, readOnly));

    /// <summary>Opens using <see cref="LiteDbOpenSettings"/>.</summary>
    public static LiteDbSession Open(LiteDbOpenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.DataSource);
        var connectionString = BuildConnectionString(settings.DataSource, settings.Password, settings.ReadOnly);
        return new LiteDbSession(new LiteDatabase(connectionString), ownsDatabase: true, settings.ReadOnly, settings.DataSource);
    }

    /// <summary>Opens using <see cref="LiteDbOptions"/>.</summary>
    public static LiteDbSession Open(LiteDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DatabasePath);
        return Open(options.DatabasePath, options.Password);
    }

    /// <summary>Wraps an existing database without ownership.</summary>
    public static LiteDbSession Wrap(ILiteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return new LiteDbSession(database, ownsDatabase: false, readOnly: false, dataSource: "(wrapped)");
    }

    /// <summary>Lists collection names.</summary>
    public IReadOnlyList<string> ListCollections() =>
        ListCollectionInfos().Select(c => c.Name).ToArray();

    /// <summary>Lists collections with document counts.</summary>
    public IReadOnlyList<LiteDbCollectionInfo> ListCollectionInfos()
    {
        return _database.GetCollectionNames()
            .OrderBy(n => n, StringComparer.Ordinal)
            .Select(name =>
            {
                long count;
                try
                {
                    count = _database.GetCollection(name).Count();
                }
                catch
                {
                    count = -1;
                }

                return new LiteDbCollectionInfo(name, count);
            })
            .ToArray();
    }

    /// <summary>Index listing via <c>$indexes</c>.</summary>
    public string GetIndexes(string? collectionName = null)
    {
        var sql = string.IsNullOrWhiteSpace(collectionName)
            ? "SELECT $ FROM $indexes"
            : $"SELECT $ FROM $indexes WHERE collection = '{EscapeLiteral(collectionName)}'";
        return Execute(sql).ToTable();
    }

    /// <summary>Index listing as a query result.</summary>
    public LiteDbQueryResult ListIndexes(string? collectionName = null)
    {
        var sql = string.IsNullOrWhiteSpace(collectionName)
            ? "SELECT $ FROM $indexes"
            : $"SELECT $ FROM $indexes WHERE collection = '{EscapeLiteral(collectionName)}'";
        return Execute(sql);
    }

    /// <summary>Key/value info for <c>.info</c>.</summary>
    public IReadOnlyList<(string Key, string Value)> GetInfo()
    {
        var rows = new List<(string, string)>
        {
            ("data_source", DataSource),
            ("read_only", _readOnly ? "yes" : "no"),
            ("collections", ListCollections().Count.ToString()),
        };

        if (!string.Equals(DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
            && !DataSource.Contains('=', StringComparison.Ordinal)
            && File.Exists(DataSource))
        {
            rows.Add(("file_bytes", new FileInfo(DataSource).Length.ToString()));
        }

        try
        {
            rows.Add(("user_version", _database.UserVersion.ToString()));
        }
        catch
        {
            // ignore
        }

        return rows;
    }

    /// <summary>Runs a LiteDB shell command.</summary>
    public LiteDbQueryResult Execute(string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        using var reader = _database.Execute(command);
        return Materialize(reader);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsDatabase)
        {
            _database.Dispose();
        }

        _disposed = true;
    }

    /// <summary>Builds a connection string matching storage conventions.</summary>
    internal static string BuildConnectionString(string dataSource, string? password, bool readOnly = false)
    {
        var path = dataSource.Contains('=', StringComparison.Ordinal)
            ? dataSource
            : "Filename=" + dataSource;

        if (!string.IsNullOrEmpty(password) &&
            !path.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            path += ";Password=" + password;
        }

        if (readOnly && !path.Contains("ReadOnly=", StringComparison.OrdinalIgnoreCase))
        {
            path += ";ReadOnly=true";
        }

        if (!path.Contains("Connection=", StringComparison.OrdinalIgnoreCase)
            && !path.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
        {
            path += ";Connection=shared";
        }

        return path;
    }

    private static LiteDbQueryResult Materialize(IBsonDataReader reader)
    {
        if (!reader.HasValues)
        {
            return new LiteDbQueryResult
            {
                Columns = [],
                Rows = [],
                RecordsAffected = 0,
            };
        }

        var documents = new List<BsonDocument>();
        var scalars = new List<string>();
        var sawDocument = false;
        var sawScalar = false;

        while (reader.Read())
        {
            var current = reader.Current;
            if (current.IsDocument)
            {
                sawDocument = true;
                documents.Add(current.AsDocument);
            }
            else
            {
                sawScalar = true;
                scalars.Add(FormatBson(current));
            }
        }

        if (sawDocument && !sawScalar)
        {
            var columns = documents
                .SelectMany(d => d.Keys)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToArray();

            var rows = new List<IReadOnlyList<string>>(documents.Count);
            foreach (var doc in documents)
            {
                var row = new string[columns.Length];
                for (var i = 0; i < columns.Length; i++)
                {
                    row[i] = doc.TryGetValue(columns[i], out var value) ? FormatBson(value) : string.Empty;
                }

                rows.Add(row);
            }

            return new LiteDbQueryResult
            {
                Columns = columns,
                Rows = rows,
                RecordsAffected = null,
            };
        }

        return new LiteDbQueryResult
        {
            Columns = ["value"],
            Rows = scalars.Select(s => (IReadOnlyList<string>)[s]).ToArray(),
            RecordsAffected = null,
        };
    }

    private static string FormatBson(BsonValue value)
    {
        if (value.IsNull)
        {
            return "NULL";
        }

        if (value.IsDocument || value.IsArray)
        {
            return JsonSerializer.Serialize(value);
        }

        return value.ToString();
    }

    private static string EscapeLiteral(string value) =>
        value.Replace("'", "''", StringComparison.Ordinal);
}

/// <summary>Collection name + document count.</summary>
/// <param name="Name">Collection name.</param>
/// <param name="DocumentCount">Count (-1 on failure).</param>
public sealed record LiteDbCollectionInfo(string Name, long DocumentCount);
