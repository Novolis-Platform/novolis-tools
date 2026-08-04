using System.CommandLine;
using Novolis.Tools.LiteDb;

var dbArgument = new Argument<string>("database")
{
    Description = "Path to a LiteDB database file, or :memory:",
};
var commandOption = new Option<string?>("--command", "-c")
{
    Description = "Run one shell statement or dot-command, then exit",
};
var passwordOption = new Option<string?>("--password", "-p")
{
    Description = "Optional AES password for the database file",
};
var modeOption = new Option<string>("--mode")
{
    Description = "Output mode: table, csv, or json",
    DefaultValueFactory = _ => "table",
};

var root = new RootCommand("novolis-litedb — interactive LiteDB shell REPL")
{
    dbArgument,
    commandOption,
    passwordOption,
    modeOption,
};

root.SetAction((parseResult, cancellationToken) =>
{
    var database = parseResult.GetValue(dbArgument)!;
    var command = parseResult.GetValue(commandOption);
    var password = parseResult.GetValue(passwordOption);
    var mode = parseResult.GetValue(modeOption) ?? "table";
    if (!IsKnownMode(mode))
    {
        Console.Error.WriteLine("Mode must be 'table', 'csv', or 'json'.");
        return Task.FromResult(2);
    }

    using var session = LiteDbSession.Open(database, password);
    var state = new ReplState { Mode = mode.ToLowerInvariant() };

    if (!string.IsNullOrWhiteSpace(command))
    {
        return Task.FromResult(ExecuteLine(session, state, command));
    }

    Console.WriteLine($"Open: {database}");
    Console.WriteLine("Enter LiteDB shell SQL or a dot-command (.help).");
    while (!cancellationToken.IsCancellationRequested)
    {
        Console.Write("litedb> ");
        var line = Console.ReadLine();
        if (line is null)
        {
            break;
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        var code = ExecuteLine(session, state, line.Trim());
        if (code == 99)
        {
            break;
        }
    }

    return Task.FromResult(0);
});

return await root.Parse(args).InvokeAsync();

static bool IsKnownMode(string mode) =>
    string.Equals(mode, "table", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(mode, "csv", StringComparison.OrdinalIgnoreCase) ||
    string.Equals(mode, "json", StringComparison.OrdinalIgnoreCase);

static int ExecuteLine(LiteDbSession session, ReplState state, string line)
{
    if (line.StartsWith('.'))
    {
        return RunDot(session, state, line);
    }

    try
    {
        var result = session.Execute(line);
        WriteResult(result, state.Mode);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static int RunDot(LiteDbSession session, ReplState state, string line)
{
    var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    var cmd = parts[0].ToLowerInvariant();
    var arg = parts.Length > 1 ? parts[1] : null;

    switch (cmd)
    {
        case ".help":
            Console.WriteLine("""
                .help                   Show this help
                .collections            List collections
                .indexes [collection]   Show indexes ($indexes)
                .mode table|csv|json    Set output format
                .quit / .exit           Leave the REPL
                """);
            return 0;
        case ".collections":
            foreach (var name in session.ListCollections())
            {
                Console.WriteLine(name);
            }

            return 0;
        case ".indexes":
            Console.WriteLine(session.GetIndexes(arg));
            return 0;
        case ".mode":
            if (arg is null || !IsKnownMode(arg))
            {
                Console.Error.WriteLine("Usage: .mode table|csv|json");
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

static void WriteResult(LiteDbQueryResult result, string mode)
{
    if (result.Columns.Count == 0)
    {
        if (result.RecordsAffected is int n)
        {
            Console.WriteLine($"{n} document(s) affected");
        }

        return;
    }

    Console.WriteLine(mode switch
    {
        "csv" => result.ToCsv(),
        "json" => result.ToJsonLines(),
        _ => result.ToTable(),
    });
}

sealed class ReplState
{
    public string Mode { get; set; } = "table";
}
