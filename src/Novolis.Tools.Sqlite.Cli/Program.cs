using System.CommandLine;
using Novolis.Tools.Sqlite;

var dbArgument = new Argument<string>("database")
{
    Description = "Path to a SQLite database file, or :memory:",
};
var commandOption = new Option<string?>("--command", "-c")
{
    Description = "Run one SQL statement or dot-command, then exit",
};
var modeOption = new Option<string>("--mode")
{
    Description = "Output mode: table or csv",
    DefaultValueFactory = _ => "table",
};

var root = new RootCommand("novolis-sqlite — interactive SQLite REPL")
{
    dbArgument,
    commandOption,
    modeOption,
};

root.SetAction(async (parseResult, cancellationToken) =>
{
    var database = parseResult.GetValue(dbArgument)!;
    var command = parseResult.GetValue(commandOption);
    var mode = parseResult.GetValue(modeOption) ?? "table";
    if (!string.Equals(mode, "table", StringComparison.OrdinalIgnoreCase) &&
        !string.Equals(mode, "csv", StringComparison.OrdinalIgnoreCase))
    {
        Console.Error.WriteLine("Mode must be 'table' or 'csv'.");
        return 2;
    }

    await using var session = SqliteSession.Open(database);
    var state = new ReplState { Mode = mode };

    if (!string.IsNullOrWhiteSpace(command))
    {
        return await ExecuteLineAsync(session, state, command, cancellationToken);
    }

    Console.WriteLine($"Open: {database}");
    Console.WriteLine("Enter SQL or a dot-command (.help).");
    while (!cancellationToken.IsCancellationRequested)
    {
        Console.Write("sqlite> ");
        var line = Console.ReadLine();
        if (line is null)
        {
            break;
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        var code = await ExecuteLineAsync(session, state, line.Trim(), cancellationToken);
        if (code == 99)
        {
            break;
        }
    }

    return 0;
});

return await root.Parse(args).InvokeAsync();

static async Task<int> ExecuteLineAsync(
    SqliteSession session,
    ReplState state,
    string line,
    CancellationToken cancellationToken)
{
    if (line.StartsWith('.'))
    {
        return await RunDotAsync(session, state, line, cancellationToken);
    }

    try
    {
        var result = await session.ExecuteAsync(line, cancellationToken);
        WriteResult(result, state.Mode);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static async Task<int> RunDotAsync(
    SqliteSession session,
    ReplState state,
    string line,
    CancellationToken cancellationToken)
{
    var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var cmd = parts[0].ToLowerInvariant();
    var arg = parts.Length > 1 ? parts[1] : null;

    switch (cmd)
    {
        case ".help":
            Console.WriteLine("""
                .help              Show this help
                .tables            List tables
                .schema [table]    Show CREATE statements
                .mode table|csv    Set output format
                .quit / .exit      Leave the REPL
                """);
            return 0;
        case ".tables":
            foreach (var table in await session.ListTablesAsync(cancellationToken))
            {
                Console.WriteLine(table);
            }

            return 0;
        case ".schema":
            Console.WriteLine(await session.GetSchemaAsync(arg, cancellationToken));
            return 0;
        case ".mode":
            if (arg is null ||
                (!string.Equals(arg, "table", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(arg, "csv", StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine("Usage: .mode table|csv");
                return 1;
            }

            state.Mode = arg.ToLowerInvariant();
            return 0;
        case ".quit":
        case ".exit":
            return 99;
        default:
            Console.Error.WriteLine($"Unknown dot-command: {cmd}");
            return 1;
    }
}

static void WriteResult(SqliteQueryResult result, string mode)
{
    if (result.Columns.Count == 0)
    {
        if (result.RecordsAffected is int n)
        {
            Console.WriteLine($"{n} row(s) affected");
        }

        return;
    }

    Console.WriteLine(string.Equals(mode, "csv", StringComparison.OrdinalIgnoreCase)
        ? result.ToCsv()
        : result.ToTable());
}

sealed class ReplState
{
    public string Mode { get; set; } = "table";
}
