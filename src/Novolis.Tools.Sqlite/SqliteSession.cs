using System.Text;
using Microsoft.Data.Sqlite;
using Novolis.Storage.Sqlite;
using MsSqliteConnection = Microsoft.Data.Sqlite.SqliteConnection;

namespace Novolis.Tools.Sqlite;

/// <summary>
/// Tabular result of a SQL statement: column names, stringified cells, and optional
/// <see cref="RecordsAffected"/> for non-query work.
/// </summary>
/// <remarks>
/// Formatters are intentionally small and allocation-friendly for REPL output — not a
/// full reporting layer. Prefer <see cref="ToTable"/> interactively and <see cref="ToCsv"/>
/// when piping into other tools.
/// </remarks>
public sealed class SqliteQueryResult
{
    /// <summary>Column names in display order.</summary>
    public required IReadOnlyList<string> Columns { get; init; }

    /// <summary>Row values aligned to <see cref="Columns"/>; null database values become <c>NULL</c>.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }

    /// <summary>Rows affected for non-query statements; <see langword="null"/> when a result set was returned.</summary>
    public int? RecordsAffected { get; init; }

    /// <summary>Formats a fixed-width text table suitable for a terminal.</summary>
    /// <returns>A multi-line table, or a short “rows affected” message when there are no columns.</returns>
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
/// Opened SQLite connection with helpers aimed at CLI REPLs and short scripts.
/// </summary>
/// <remarks>
/// Depends on <c>Novolis.Storage.Sqlite</c> so engine versions stay aligned with repository
/// hosts that use <see cref="SqliteOptions"/> / <c>AddSqliteProvider</c>. This type is not an
/// <c>IRepository{T}</c> — it is for ad-hoc SQL inspection of the same files those providers write.
/// </remarks>
public sealed class SqliteSession : IAsyncDisposable, IDisposable
{
    private readonly MsSqliteConnection _connection;
    private bool _disposed;

    private SqliteSession(MsSqliteConnection connection)
    {
        _connection = connection;
    }

    /// <summary>
    /// Opens a database file (created if missing) or an in-memory database when
    /// <paramref name="dataSource"/> is <c>:memory:</c>.
    /// </summary>
    /// <param name="dataSource">File path or <c>:memory:</c>.</param>
    /// <returns>An open session; dispose when finished.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="dataSource"/> is null or whitespace.</exception>
    public static SqliteSession Open(string dataSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataSource);
        var connection = new MsSqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = dataSource,
            Mode = dataSource == ":memory:" ? SqliteOpenMode.Memory : SqliteOpenMode.ReadWriteCreate,
        }.ToString());
        connection.Open();
        return new SqliteSession(connection);
    }

    /// <summary>
    /// Opens using the same options shape as <c>Novolis.Storage.Sqlite</c>
    /// (<c>Data Source=…</c> connection strings).
    /// </summary>
    /// <param name="options">Storage SQLite options; connection string is required.</param>
    /// <returns>An open session; dispose when finished.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the connection string is missing.</exception>
    public static SqliteSession Open(SqliteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.ConnectionString);
        var connection = new MsSqliteConnection(options.ConnectionString);
        connection.Open();
        return new SqliteSession(connection);
    }

    /// <summary>Lists user table names (excludes <c>sqlite_%</c> internals).</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Sorted table names.</returns>
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

    /// <summary>Returns <c>sqlite_master.sql</c> for one table or all user objects with DDL.</summary>
    /// <param name="tableName">Optional table or object name; omit to list all user DDL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>CREATE statements separated by blank lines, each ending with <c>;</c>.</returns>
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
    /// <param name="sql">One SQL statement.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result set or an affected-row count.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="sql"/> is null or whitespace.</exception>
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
