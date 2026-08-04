using System.Text;
using Microsoft.Data.Sqlite;

namespace Novolis.Tools.Sqlite;

/// <summary>Result of a SQL statement that returns a grid.</summary>
public sealed class SqliteQueryResult
{
    /// <summary>Column names.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>Row values aligned to <see cref="Columns"/>.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    /// <summary>Rows affected for non-query statements; null for result sets.</summary>
    public int? RecordsAffected { get; init; }

    /// <summary>Formats as a fixed-ish text table.</summary>
    public string ToTable()
    {
        if (Columns.Count == 0)
        {
            return RecordsAffected is int n ? $"{n} row(s) affected" : string.Empty;
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

        sb.Append($"{Rows.Count} row(s)");
        return sb.ToString();
    }

    /// <summary>Formats as CSV (RFC4180-ish quoting).</summary>
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

/// <summary>Opened SQLite connection with helper queries for CLI use.</summary>
public sealed class SqliteSession : IAsyncDisposable, IDisposable
{
    private readonly SqliteConnection _connection;
    private bool _disposed;

    private SqliteSession(SqliteConnection connection)
    {
        _connection = connection;
    }

    /// <summary>Opens a database file (created if missing) or <c>:memory:</c>.</summary>
    public static SqliteSession Open(string dataSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSource);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dataSource,
            Mode = dataSource == ":memory:" ? SqliteOpenMode.Memory : SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        connection.Open();
        return new SqliteSession(connection);
    }

    /// <summary>Lists user table names.</summary>
    public async Task<IReadOnlyList<string>> ListTablesAsync(CancellationToken cancellationToken = default)
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
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    /// <summary>Returns <c>sqlite_master.sql</c> for one table or all user tables.</summary>
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

    /// <summary>Executes SQL; returns a grid when the statement yields rows.</summary>
    public async Task<SqliteQueryResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
        await using var command = _connection.CreateCommand();
        command.CommandText = sql;

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

        // Non-query path: ExecuteReader already ran; RecordsAffected is available.
        return new SqliteQueryResult
        {
            Columns = [],
            Rows = [],
            RecordsAffected = reader.RecordsAffected,
        };
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
}
