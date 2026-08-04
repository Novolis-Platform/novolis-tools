using System.Text;
using LiteDB;
using Novolis.Storage.LiteDb;

namespace Novolis.Tools.LiteDb;

/// <summary>
/// Tabular projection of a LiteDB shell command: column names, stringified cells, and optional
/// <see cref="RecordsAffected"/> when the reader yields no values.
/// </summary>
/// <remarks>
/// Document-shaped rows are flattened to columns (union of field names across the batch).
/// Nested documents and arrays are rendered as compact JSON via <see cref="BsonValue.ToString"/>.
/// </remarks>
public sealed class LiteDbQueryResult
{
    /// <summary>Column names in display order.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>Row values aligned to <see cref="Columns"/>; missing fields become empty strings; BSON nulls become <c>NULL</c>.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    /// <summary>Hint for non-result commands; <see langword="null"/> when a result set was returned.</summary>
    public int? RecordsAffected { get; init; }

    /// <summary>Formats a fixed-width text table suitable for a terminal.</summary>
    /// <returns>A multi-line table, a short status line, or empty when there is nothing to show.</returns>
    public string ToTable()
    {
        if (Columns.Count == 0)
        {
            return RecordsAffected is int n ? $"{n} document(s) affected" : string.Empty;
        }

        var widths = Columns.Select((c, i) =>
            Math.Max(c.Length, Rows.Count == 0 ? 0 : Rows.Max(r => r[i].Length))).ToArray();

        var sb = new StringBuilder();
        sb.AppendLine(FormatRow(Columns, widths));
        sb.AppendLine(string.Join('+', widths.Select(w => new string('-', w + 2))));
        foreach (var row in Rows)
        {
            sb.AppendLine(FormatRow(row, widths));
        }

        sb.Append($"{Rows.Count} document(s)");
        return sb.ToString();
    }

    /// <summary>Formats as CSV with RFC4180-ish quoting for commas, quotes, and newlines.</summary>
    /// <returns>CSV text without a trailing blank line.</returns>
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
    /// <returns>NDJSON when there are columns; otherwise empty or a short status line.</returns>
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

    private static string FormatRow(IReadOnlyList<string> cells, int[] widths)
    {
        var parts = new string[cells.Count];
        for (var i = 0; i < cells.Count; i++)
        {
            parts[i] = " " + cells[i].PadRight(widths[i]) + " ";
        }

        return string.Join('|', parts);
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

/// <summary>
/// Opened LiteDB database with helpers aimed at CLI REPLs and short scripts.
/// </summary>
/// <remarks>
/// Depends on <c>Novolis.Storage.LiteDb</c> so engine and connection-string conventions stay
/// aligned with repository hosts that use <see cref="LiteDbOptions"/> / <c>AddLiteDbProvider</c>.
/// This type is not an <c>IRepository{T}</c> — it is for ad-hoc shell SQL against the same files
/// those providers write.
/// </remarks>
public sealed class LiteDbSession : IDisposable
{
    private readonly ILiteDatabase _database;
    private readonly bool _ownsDatabase;
    private bool _disposed;

    private LiteDbSession(ILiteDatabase database, bool ownsDatabase)
    {
        _database = database;
        _ownsDatabase = ownsDatabase;
    }

    /// <summary>
    /// Opens a database file (created if missing) or an in-memory database when
    /// <paramref name="dataSource"/> is <c>:memory:</c>.
    /// </summary>
    /// <param name="dataSource">File path, <c>:memory:</c>, or a full LiteDB connection string containing <c>=</c>.</param>
    /// <param name="password">Optional AES password; ignored when <paramref name="dataSource"/> already sets <c>Password=</c>.</param>
    /// <returns>An open session; dispose when finished.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="dataSource"/> is null or whitespace.</exception>
    public static LiteDbSession Open(string dataSource, string? password = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSource);
        var connectionString = BuildConnectionString(dataSource, password);
        return new LiteDbSession(new LiteDatabase(connectionString), ownsDatabase: true);
    }

    /// <summary>
    /// Opens using the same <see cref="LiteDbOptions"/> shape as <c>Novolis.Storage.LiteDb</c>.
    /// </summary>
    /// <param name="options">Storage LiteDB options; <see cref="LiteDbOptions.DatabasePath"/> is required.</param>
    /// <returns>An open session; dispose when finished.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the database path is missing.</exception>
    public static LiteDbSession Open(LiteDbOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.DatabasePath);
        return Open(options.DatabasePath, options.Password);
    }

    /// <summary>
    /// Wraps an existing <see cref="ILiteDatabase"/> (for example the singleton registered by
    /// <c>AddLiteDbProvider</c>) without taking ownership.
    /// </summary>
    /// <param name="database">Open LiteDB instance.</param>
    /// <returns>A session that does not dispose <paramref name="database"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="database"/> is null.</exception>
    public static LiteDbSession Wrap(ILiteDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        return new LiteDbSession(database, ownsDatabase: false);
    }

    /// <summary>Lists collection names in sorted order.</summary>
    /// <returns>Collection names present in the database.</returns>
    public IReadOnlyList<string> ListCollections()
    {
        return _database.GetCollectionNames().OrderBy(n => n, StringComparer.Ordinal).ToArray();
    }

    /// <summary>
    /// Describes indexes via LiteDB’s <c>$indexes</c> system collection, optionally filtered to one collection.
    /// </summary>
    /// <param name="collectionName">Optional collection name; omit to list all indexes.</param>
    /// <returns>A table-formatted index listing (or empty when none match).</returns>
    public string GetIndexes(string? collectionName = null)
    {
        var sql = string.IsNullOrWhiteSpace(collectionName)
            ? "SELECT $ FROM $indexes"
            : $"SELECT $ FROM $indexes WHERE collection = '{EscapeLiteral(collectionName)}'";
        return Execute(sql).ToTable();
    }

    /// <summary>
    /// Runs a LiteDB shell command (SQL-like <c>SELECT</c>/<c>INSERT</c>/<c>UPDATE</c>/<c>DELETE</c>/…).
    /// </summary>
    /// <param name="command">Shell SQL text.</param>
    /// <returns>A flattened result grid or an affected-count placeholder.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="command"/> is null or whitespace.</exception>
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

    /// <summary>
    /// Builds a connection string matching <c>Novolis.Storage.LiteDb</c> conventions:
    /// <c>Filename=</c> when needed, optional password, and <c>Connection=shared</c> for file paths.
    /// </summary>
    internal static string BuildConnectionString(string dataSource, string? password)
    {
        var path = dataSource.Contains('=', StringComparison.Ordinal)
            ? dataSource
            : "Filename=" + dataSource;

        if (!string.IsNullOrEmpty(password) &&
            !path.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            path += ";Password=" + password;
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
