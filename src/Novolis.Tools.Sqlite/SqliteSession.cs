using System.Text;
using Microsoft.Data.Sqlite;
using Novolis.Storage.Sqlite;
using MsSqliteConnection = Microsoft.Data.Sqlite.SqliteConnection;

namespace Novolis.Tools.Sqlite;

/// <summary>Open flags for <see cref="SqliteSession"/>.</summary>
/// <param name="DataSource">File path or <c>:memory:</c> (already resolved by the caller).</param>
/// <param name="ReadOnly">Open existing file read-only (never creates).</param>
public sealed record SqliteOpenSettings(string DataSource, bool ReadOnly = false);

/// <summary>Tabular result of a SQL statement.</summary>
public sealed class SqliteQueryResult
{
    /// <summary>Column names in display order.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>Row values aligned to <see cref="Columns"/>; nulls become <c>NULL</c>.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    /// <summary>Rows affected for non-query statements; null when a result set was returned.</summary>
    public int? RecordsAffected { get; init; }

    /// <summary>Formats a fixed-width text table.</summary>
    public string ToTable()
    {
        if (Columns.Count == 0)
        {
            return RecordsAffected is int n ? $"{n} row(s) affected" : string.Empty;
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

        sb.Append($"{Rows.Count} row(s)");
        return sb.ToString();
    }

    /// <summary>Formats as CSV with RFC4180-ish quoting.</summary>
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

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
        {
            return "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return value;
    }
}

/// <summary>Opened SQLite connection with CLI-oriented helpers.</summary>
public sealed class SqliteSession : IAsyncDisposable, IDisposable
{
    private readonly MsSqliteConnection _connection;
    private readonly bool _readOnly;
    private bool _disposed;

    private SqliteSession(MsSqliteConnection connection, bool readOnly, string dataSource)
    {
        _connection = connection;
        _readOnly = readOnly;
        DataSource = dataSource;
    }

    /// <summary>True when the connection is read-only.</summary>
    public bool IsReadOnly => _readOnly;

    /// <summary>Resolved data source path or <c>:memory:</c>.</summary>
    public string DataSource { get; }

    /// <summary>
    /// Opens a database. Prefer resolving paths with CLI <c>OpenGuards</c> first so missing
    /// files are not created by accident.
    /// </summary>
    public static SqliteSession Open(string dataSource, bool readOnly = false)
        => Open(new SqliteOpenSettings(dataSource, readOnly));

    /// <summary>Opens using <see cref="SqliteOpenSettings"/>.</summary>
    public static SqliteSession Open(SqliteOpenSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(settings.DataSource);
        var source = settings.DataSource;

        MsSqliteConnection connection;
        string dataSourceLabel;
        if (source.Contains('=', StringComparison.Ordinal)
            && !string.Equals(source, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            // Full connection string (Data Source=…;Mode=…).
            var builder = new SqliteConnectionStringBuilder(source);
            if (settings.ReadOnly)
            {
                builder.Mode = SqliteOpenMode.ReadOnly;
            }

            builder.Pooling = false;
            dataSourceLabel = builder.DataSource;
            connection = new MsSqliteConnection(builder.ToString());
        }
        else
        {
            var mode = string.Equals(source, ":memory:", StringComparison.OrdinalIgnoreCase)
                ? SqliteOpenMode.Memory
                : settings.ReadOnly
                    ? SqliteOpenMode.ReadOnly
                    : SqliteOpenMode.ReadWriteCreate;

            connection = new MsSqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = source,
                Mode = mode,
                Pooling = false,
            }.ToString());
            dataSourceLabel = source;
        }

        connection.Open();

        // foreign_keys is a per-connection setting (works in read-only too).
        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        return new SqliteSession(connection, settings.ReadOnly, dataSourceLabel);
    }

    /// <summary>Opens using <see cref="SqliteOptions"/> (storage connection string).</summary>
    public static SqliteSession Open(SqliteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);
        var connection = new MsSqliteConnection(options.ConnectionString);
        connection.Open();
        return new SqliteSession(connection, readOnly: false, options.ConnectionString);
    }

    /// <summary>Lists user table names.</summary>
    public async Task<IReadOnlyList<string>> ListTablesAsync(CancellationToken cancellationToken = default)
    {
        var infos = await ListTableInfosAsync(cancellationToken);
        return infos.Select(i => i.Name).ToArray();
    }

    /// <summary>Lists tables with approximate row counts.</summary>
    public async Task<IReadOnlyList<SqliteTableInfo>> ListTableInfosAsync(CancellationToken cancellationToken = default)
    {
        await using var command = _connection.CreateCommand();
        command.CommandText =
            """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            ORDER BY name;
            """;
        var names = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                names.Add(reader.GetString(0));
            }
        }

        var list = new List<SqliteTableInfo>(names.Count);
        foreach (var name in names)
        {
            long count;
            try
            {
                await using var countCmd = _connection.CreateCommand();
                countCmd.CommandText = $"SELECT COUNT(*) FROM \"{EscapeIdent(name)}\";";
                var scalar = await countCmd.ExecuteScalarAsync(cancellationToken);
                count = Convert.ToInt64(scalar);
            }
            catch
            {
                count = -1;
            }

            list.Add(new SqliteTableInfo(name, count));
        }

        return list;
    }

    /// <summary>Lists indexes (optionally for one table).</summary>
    public async Task<SqliteQueryResult> ListIndexesAsync(string? tableName = null, CancellationToken cancellationToken = default)
    {
        var sql = string.IsNullOrWhiteSpace(tableName)
            ? """
              SELECT name AS index_name, tbl_name AS table_name, sql
              FROM sqlite_master
              WHERE type = 'index' AND name NOT LIKE 'sqlite_%'
              ORDER BY tbl_name, name;
              """
            : """
              SELECT name AS index_name, tbl_name AS table_name, sql
              FROM sqlite_master
              WHERE type = 'index' AND tbl_name = $name AND name NOT LIKE 'sqlite_%'
              ORDER BY name;
              """;
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        if (!string.IsNullOrWhiteSpace(tableName))
        {
            command.Parameters.AddWithValue("$name", tableName);
        }

        return await ReadGridAsync(command, cancellationToken);
    }

    /// <summary>Returns DDL for one object or all user objects.</summary>
    public async Task<string> GetSchemaAsync(string? tableName = null, CancellationToken cancellationToken = default)
    {
        await using var command = _connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(tableName))
        {
            command.CommandText =
                """
                SELECT sql
                FROM sqlite_master
                WHERE sql IS NOT NULL AND name NOT LIKE 'sqlite_%'
                ORDER BY type, name;
                """;
        }
        else
        {
            command.CommandText =
                """
                SELECT sql
                FROM sqlite_master
                WHERE name = $name AND sql IS NOT NULL;
                """;
            command.Parameters.AddWithValue("$name", tableName);
        }

        var parts = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                parts.Add(reader.GetString(0) + ";");
            }
        }

        return string.Join(Environment.NewLine + Environment.NewLine, parts);
    }

    /// <summary>Key/value database info for banners and <c>.info</c>.</summary>
    public async Task<IReadOnlyList<(string Key, string Value)>> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var rows = new List<(string, string)>
        {
            ("data_source", DataSource),
            ("read_only", _readOnly ? "yes" : "no"),
        };

        async Task AddPragma(string name)
        {
            try
            {
                await using var cmd = _connection.CreateCommand();
                cmd.CommandText = "PRAGMA " + name + ";";
                var value = await cmd.ExecuteScalarAsync(cancellationToken);
                rows.Add((name, Convert.ToString(value) ?? string.Empty));
            }
            catch
            {
                // ignore unsupported pragmas
            }
        }

        await AddPragma("page_count");
        await AddPragma("page_size");
        await AddPragma("journal_mode");
        await AddPragma("foreign_keys");
        await AddPragma("user_version");

        if (!string.Equals(DataSource, ":memory:", StringComparison.OrdinalIgnoreCase)
            && File.Exists(DataSource))
        {
            rows.Add(("file_bytes", new FileInfo(DataSource).Length.ToString()));
        }

        var tables = await ListTablesAsync(cancellationToken);
        rows.Add(("tables", tables.Count.ToString()));
        return rows;
    }

    /// <summary>Executes SQL; returns a grid when the statement yields rows.</summary>
    /// <remarks>
    /// When opened read-only, the SQLite engine rejects writes. Callers should still
    /// pre-check with CLI heuristics for clearer errors.
    /// </remarks>
    public async Task<SqliteQueryResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return await ReadGridAsync(command, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _connection.Dispose();
        _disposed = true;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _connection.DisposeAsync();
        _disposed = true;
    }

    private static async Task<SqliteQueryResult> ReadGridAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (reader.FieldCount > 0)
        {
            var columns = new string[reader.FieldCount];
            for (var i = 0; i < reader.FieldCount; i++)
            {
                columns[i] = reader.GetName(i);
            }

            var rows = new List<IReadOnlyList<string>>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new string[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = reader.IsDBNull(i) ? "NULL" : Convert.ToString(reader.GetValue(i)) ?? string.Empty;
                }

                rows.Add(row);
            }

            return new SqliteQueryResult
            {
                Columns = columns,
                Rows = rows,
                RecordsAffected = null,
            };
        }

        return new SqliteQueryResult
        {
            Columns = [],
            Rows = [],
            RecordsAffected = reader.RecordsAffected,
        };
    }

    private static string EscapeIdent(string name) => name.Replace("\"", "\"\"", StringComparison.Ordinal);
}

/// <summary>Table name + row count (-1 when count failed).</summary>
/// <param name="Name">Table name.</param>
/// <param name="RowCount">Approximate COUNT(*).</param>
public sealed record SqliteTableInfo(string Name, long RowCount);
