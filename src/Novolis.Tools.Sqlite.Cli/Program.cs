using System.CommandLine;
using System.Diagnostics;
using Novolis.Tools.Cli;
using Novolis.Tools.Sqlite;
using Spectre.Console;

var console = AnsiConsole.Console;

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
    Description = $"Output mode: {OutputModes.HelpList}",
    DefaultValueFactory = _ => "table",
};
var createOption = new Option<bool>("--create")
{
    Description = "Allow creating a missing database file (default: refuse — pit of success)",
    DefaultValueFactory = _ => false,
};
var readOnlyOption = new Option<bool>("--read-only")
{
    Description = "Open an existing database read-only",
    DefaultValueFactory = _ => false,
};
var limitOption = new Option<int>("--limit")
{
    Description = "Max rows to display (0 = unlimited). Default 200.",
    DefaultValueFactory = _ => 200,
};
var noConfirmOption = new Option<bool>("--no-confirm")
{
    Description = "Skip confirmation for destructive SQL",
    DefaultValueFactory = _ => false,
};
var noTimerOption = new Option<bool>("--no-timer")
{
    Description = "Hide query timings",
    DefaultValueFactory = _ => false,
};

var root = new RootCommand("""
    novolis-sqlite — Spectre SQLite REPL (pit of success defaults)

    Examples:
      novolis-sqlite app.db --read-only
      novolis-sqlite app.db --create
      novolis-sqlite :memory: -c "SELECT 1 AS n;"
      novolis-sqlite app.db --mode json -c ".tables"
    """)
{
    dbArgument,
    commandOption,
    modeOption,
    createOption,
    readOnlyOption,
    limitOption,
    noConfirmOption,
    noTimerOption,
};

root.SetAction(async (parseResult, cancellationToken) =>
{
    var database = parseResult.GetValue(dbArgument)!;
    var command = parseResult.GetValue(commandOption);
    var modeText = parseResult.GetValue(modeOption) ?? "table";
    var create = parseResult.GetValue(createOption);
    var readOnly = parseResult.GetValue(readOnlyOption);
    var limit = parseResult.GetValue(limitOption);
    var noConfirm = parseResult.GetValue(noConfirmOption);
    var noTimer = parseResult.GetValue(noTimerOption);

    if (!OutputModes.TryParse(modeText, out var mode))
    {
        ReplChrome.Error(console, $"Mode must be {OutputModes.HelpList}.");
        return ExitCodes.Usage;
    }

    string source;
    string label;
    try
    {
        source = OpenGuards.ResolveDataSource(database, create, readOnly, out label);
    }
    catch (Exception ex)
    {
        ReplChrome.Error(console, ex.Message);
        return ExitCodes.Usage;
    }

    if (create && File.Exists(source) && source is not ":memory:")
    {
        ReplChrome.Warn(console, "File already exists; --create is unnecessary (opening normally).");
    }

    await using var session = SqliteSession.Open(source, readOnly);
    var prefs = new ReplPreferences
    {
        Mode = mode,
        Limit = limit,
        Timer = !noTimer,
        ConfirmDestructive = !noConfirm,
        ReadOnly = readOnly,
        DatabaseLabel = label,
    };
    var help = BuildHelp();

    if (!string.IsNullOrWhiteSpace(command))
    {
        return await ExecuteLineAsync(console, session, prefs, help, command, cancellationToken);
    }

    ReplChrome.Banner(
        console,
        "novolis-sqlite",
        label,
        prefs,
        "Type .help — multi-line SQL ends with ; — foreign_keys=ON — missing files need --create");

    while (!cancellationToken.IsCancellationRequested)
    {
        var line = ReplChrome.ReadStatement(console, "sqlite>");
        if (line is null)
        {
            break;
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        var code = await ExecuteLineAsync(console, session, prefs, help, line.Trim(), cancellationToken);
        if (code == ExitCodes.Quit)
        {
            ReplChrome.Ok(console, "bye");
            break;
        }
    }

    return ExitCodes.Ok;
});

return await root.Parse(args).InvokeAsync();

static DotHelpCatalog BuildHelp() => new DotHelpCatalog()
    .Add(".help", "Show commands or details for one topic", "Pass a command name for examples and details.", ".help tables")
    .Add(".tables", "List tables with row counts", "Shows user tables (excludes sqlite_%).", ".tables")
    .Add(".schema", "Show CREATE DDL", "Optional table/index name.", ".schema items")
    .Add(".indexes", "List indexes", "Optional table filter.", ".indexes items")
    .Add(".info", "Database pragmas and file size", "page_count, journal_mode, foreign_keys, …", ".info")
    .Add(".mode", $"Set output mode ({OutputModes.HelpList})", "table uses Spectre; csv/json for piping.", ".mode json")
    .Add(".limit", "Max rows to display (0 = unlimited)", "Default 200 prevents terminal floods.", ".limit 50")
    .Add(".timer", "Toggle query timings", "on|off", ".timer off")
    .Add(".confirm", "Toggle destructive SQL confirmation", "on|off — DROP/DELETE/ALTER/VACUUM", ".confirm off")
    .Add(".export", "Export last result to a file", "Extension picks csv/json/table.", ".export out.csv")
    .Add(".quit", "Leave the REPL", "Also .exit", ".quit");

static async Task<int> ExecuteLineAsync(
    IAnsiConsole console,
    SqliteSession session,
    ReplPreferences prefs,
    DotHelpCatalog help,
    string line,
    CancellationToken cancellationToken)
{
    if (line.StartsWith('.'))
    {
        return await RunDotAsync(console, session, prefs, help, line, cancellationToken);
    }

    if (prefs.ReadOnly && ReplChrome.IsWriteSql(line))
    {
        ReplChrome.Error(console, "Session is read-only; refusing write SQL.");
        return ExitCodes.Failure;
    }

    if (ReplChrome.IsDestructiveSql(line)
        && !ReplChrome.ConfirmDestructive(console, prefs, SummarizeSql(line)))
    {
        ReplChrome.Warn(console, "Cancelled.");
        return ExitCodes.Ok;
    }

        try
        {
            var sw = Stopwatch.StartNew();
            var raw = await session.ExecuteAsync(line, cancellationToken);
            sw.Stop();
            var full = TabularResult.Grid(raw.Columns, raw.Rows, "row", truncated: false, sw.Elapsed);
            if (raw.Columns.Count == 0)
            {
                full = TabularResult.Affected(raw.RecordsAffected ?? 0, "row", sw.Elapsed);
            }

            LastResult.Full = full;
            var shaped = ResultShaping.Shape(raw.Columns, raw.Rows, raw.RecordsAffected, "row", prefs, sw.Elapsed);
            LastResult.Display = shaped;
            ResultPrinter.Write(console, shaped, prefs.Mode, prefs);
            return ExitCodes.Ok;
        }
    catch (Exception ex)
    {
        ReplChrome.Error(console, ex.Message);
        return ExitCodes.Failure;
    }
}

static async Task<int> RunDotAsync(
    IAnsiConsole console,
    SqliteSession session,
    ReplPreferences prefs,
    DotHelpCatalog help,
    string line,
    CancellationToken cancellationToken)
{
    var parts = SplitDot(line);
    var cmd = parts.Command;
    var arg = parts.Argument;

    switch (cmd)
    {
        case ".help":
            ReplChrome.ShowHelp(console, help, arg);
            return ExitCodes.Ok;
        case ".tables":
        {
            var infos = await session.ListTableInfosAsync(cancellationToken);
            var table = new Table().Border(TableBorder.Rounded).AddColumn("table").AddColumn("rows");
            foreach (var info in infos)
            {
                table.AddRow(
                    Markup.Escape(info.Name),
                    info.RowCount < 0 ? "[dim]?[/]" : info.RowCount.ToString());
            }

            if (infos.Count == 0)
            {
                ReplChrome.Info(console, "(no user tables)");
            }
            else
            {
                console.Write(table);
                console.WriteLine();
            }

            return ExitCodes.Ok;
        }
        case ".schema":
        {
            var ddl = await session.GetSchemaAsync(arg, cancellationToken);
            if (string.IsNullOrWhiteSpace(ddl))
            {
                ReplChrome.Info(console, "(no schema)");
            }
            else
            {
                console.Write(new Panel(new Markup($"[grey]{Markup.Escape(ddl)}[/]"))
                    .Header("[bold]schema[/]")
                    .BorderColor(Color.Grey));
            }

            return ExitCodes.Ok;
        }
        case ".indexes":
        {
            var raw = await session.ListIndexesAsync(arg, cancellationToken);
            var shaped = ResultShaping.Shape(raw.Columns, raw.Rows, raw.RecordsAffected, "row", prefs, TimeSpan.Zero);
            ResultPrinter.Write(console, shaped, prefs.Mode, prefs);
            LastResult.Full = TabularResult.Grid(raw.Columns, raw.Rows, "row");
            LastResult.Display = shaped;
            return ExitCodes.Ok;
        }
        case ".info":
        {
            var info = await session.GetInfoAsync(cancellationToken);
            var table = new Table().Border(TableBorder.Simple).HideHeaders().AddColumn("k").AddColumn("v");
            foreach (var (key, value) in info)
            {
                table.AddRow($"[aqua]{Markup.Escape(key)}[/]", Markup.Escape(value));
            }

            console.Write(table);
            console.WriteLine();
            return ExitCodes.Ok;
        }
        case ".mode":
            if (arg is null || !OutputModes.TryParse(arg, out var mode))
            {
                ReplChrome.Error(console, $"Usage: .mode {OutputModes.HelpList}");
                return ExitCodes.Failure;
            }

            prefs.Mode = mode;
            ReplChrome.Ok(console, $"mode = {OutputModes.Format(mode)}");
            return ExitCodes.Ok;
        case ".limit":
            if (arg is null || !int.TryParse(arg, out var limit) || limit < 0)
            {
                ReplChrome.Error(console, "Usage: .limit <n>   (0 = unlimited)");
                return ExitCodes.Failure;
            }

            prefs.Limit = limit;
            ReplChrome.Ok(console, $"limit = {(limit == 0 ? "unlimited" : limit.ToString())}");
            return ExitCodes.Ok;
        case ".timer":
            if (!TryOnOff(arg, out var timerOn))
            {
                ReplChrome.Error(console, "Usage: .timer on|off");
                return ExitCodes.Failure;
            }

            prefs.Timer = timerOn;
            ReplChrome.Ok(console, $"timer = {(timerOn ? "on" : "off")}");
            return ExitCodes.Ok;
        case ".confirm":
            if (!TryOnOff(arg, out var confirmOn))
            {
                ReplChrome.Error(console, "Usage: .confirm on|off");
                return ExitCodes.Failure;
            }

            prefs.ConfirmDestructive = confirmOn;
            ReplChrome.Ok(console, $"confirm = {(confirmOn ? "on" : "off")}");
            return ExitCodes.Ok;
        case ".export":
            if (string.IsNullOrWhiteSpace(arg))
            {
                ReplChrome.Error(console, "Usage: .export <path>");
                return ExitCodes.Failure;
            }

            if (LastResult.Full is null)
            {
                ReplChrome.Error(console, "No result to export yet.");
                return ExitCodes.Failure;
            }

            try
            {
                ResultPrinter.Export(arg, LastResult.Full);
                ReplChrome.Ok(console, $"wrote {Path.GetFullPath(arg)} ({LastResult.Full.Rows.Count} rows)");
                return ExitCodes.Ok;
            }
            catch (Exception ex)
            {
                ReplChrome.Error(console, ex.Message);
                return ExitCodes.Failure;
            }
        case ".quit":
        case ".exit":
            return ExitCodes.Quit;
        default:
            ReplChrome.Error(console, $"Unknown dot-command: {cmd}");
            ReplChrome.Suggest(console, help.Names, cmd);
            return ExitCodes.Failure;
    }
}

static (string Command, string? Argument) SplitDot(string line)
{
    var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    return (parts[0].ToLowerInvariant(), parts.Length > 1 ? parts[1] : null);
}

static bool TryOnOff(string? arg, out bool value)
{
    value = false;
    if (arg is null)
    {
        return false;
    }

    switch (arg.Trim().ToLowerInvariant())
    {
        case "on":
        case "true":
        case "1":
            value = true;
            return true;
        case "off":
        case "false":
        case "0":
            value = false;
            return true;
        default:
            return false;
    }
}

static string SummarizeSql(string sql)
{
    var one = sql.Replace('\r', ' ').Replace('\n', ' ').Trim();
    return one.Length <= 80 ? one : one[..77] + "...";
}

static class LastResult
{
    public static TabularResult? Full { get; set; }
    public static TabularResult? Display { get; set; }
}
