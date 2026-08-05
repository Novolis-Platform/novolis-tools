using System.CommandLine;
using System.Diagnostics;
using Novolis.Tools.Cli;
using Novolis.Tools.LiteDb;
using Spectre.Console;

var console = AnsiConsole.Console;

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
    Description = "Max documents to display (0 = unlimited). Default 200.",
    DefaultValueFactory = _ => 200,
};
var noConfirmOption = new Option<bool>("--no-confirm")
{
    Description = "Skip confirmation for destructive shell SQL",
    DefaultValueFactory = _ => false,
};
var noTimerOption = new Option<bool>("--no-timer")
{
    Description = "Hide query timings",
    DefaultValueFactory = _ => false,
};

var root = new RootCommand("""
    novolis-litedb — Spectre LiteDB shell REPL (pit of success defaults)

    Examples:
      novolis-litedb app.db --read-only
      novolis-litedb app.db --create
      novolis-litedb :memory: -c "SELECT 1"
      novolis-litedb app.db --mode json -c ".collections"
    """)
{
    dbArgument,
    commandOption,
    passwordOption,
    modeOption,
    createOption,
    readOnlyOption,
    limitOption,
    noConfirmOption,
    noTimerOption,
};

root.SetAction((parseResult, cancellationToken) =>
{
    var database = parseResult.GetValue(dbArgument)!;
    var command = parseResult.GetValue(commandOption);
    var password = parseResult.GetValue(passwordOption);
    var modeText = parseResult.GetValue(modeOption) ?? "table";
    var create = parseResult.GetValue(createOption);
    var readOnly = parseResult.GetValue(readOnlyOption);
    var limit = parseResult.GetValue(limitOption);
    var noConfirm = parseResult.GetValue(noConfirmOption);
    var noTimer = parseResult.GetValue(noTimerOption);

    if (!OutputModes.TryParse(modeText, out var mode))
    {
        ReplChrome.Error(console, $"Mode must be {OutputModes.HelpList}.");
        return Task.FromResult(ExitCodes.Usage);
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
        return Task.FromResult(ExitCodes.Usage);
    }

    if (create && File.Exists(source) && source is not ":memory:")
    {
        ReplChrome.Warn(console, "File already exists; --create is unnecessary (opening normally).");
    }

    using var session = LiteDbSession.Open(source, password, readOnly);
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
        return Task.FromResult(ExecuteLine(console, session, prefs, help, command));
    }

    ReplChrome.Banner(
        console,
        "novolis-litedb",
        label,
        prefs,
        "Type .help — multi-line shell SQL ends with ; — missing files need --create");

    while (!cancellationToken.IsCancellationRequested)
    {
        var line = ReplChrome.ReadStatement(console, "litedb>");
        if (line is null)
        {
            break;
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            continue;
        }

        var code = ExecuteLine(console, session, prefs, help, line.Trim());
        if (code == ExitCodes.Quit)
        {
            ReplChrome.Ok(console, "bye");
            break;
        }
    }

    return Task.FromResult(ExitCodes.Ok);
});

return await root.Parse(args).InvokeAsync();

static DotHelpCatalog BuildHelp() => new DotHelpCatalog()
    .Add(".help", "Show commands or details for one topic", "Pass a command name for examples and details.", ".help collections")
    .Add(".collections", "List collections with document counts", "Sorted collection inventory.", ".collections")
    .Add(".indexes", "Show indexes ($indexes)", "Optional collection filter.", ".indexes items")
    .Add(".info", "Database path, size, user_version", "Quick health snapshot.", ".info")
    .Add(".mode", $"Set output mode ({OutputModes.HelpList})", "table uses Spectre; csv/json for piping.", ".mode json")
    .Add(".limit", "Max documents to display (0 = unlimited)", "Default 200 prevents terminal floods.", ".limit 50")
    .Add(".timer", "Toggle query timings", "on|off", ".timer off")
    .Add(".confirm", "Toggle destructive SQL confirmation", "on|off — DROP/DELETE/ALTER", ".confirm off")
    .Add(".export", "Export last result to a file", "Extension picks csv/json/table.", ".export out.json")
    .Add(".quit", "Leave the REPL", "Also .exit", ".quit");

static int ExecuteLine(
    IAnsiConsole console,
    LiteDbSession session,
    ReplPreferences prefs,
    DotHelpCatalog help,
    string line)
{
    if (line.StartsWith('.'))
    {
        return RunDot(console, session, prefs, help, line);
    }

    if (prefs.ReadOnly && ReplChrome.IsWriteSql(line))
    {
        ReplChrome.Error(console, "Session is read-only; refusing write shell SQL.");
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
        var raw = session.Execute(line);
        sw.Stop();
        var shaped = ResultShaping.Shape(raw.Columns, raw.Rows, raw.RecordsAffected, "document", prefs, sw.Elapsed);
        LastResult.Current = shaped;
        ResultPrinter.Write(console, shaped, prefs.Mode, prefs);
        return ExitCodes.Ok;
    }
    catch (Exception ex)
    {
        ReplChrome.Error(console, ex.Message);
        return ExitCodes.Failure;
    }
}

static int RunDot(
    IAnsiConsole console,
    LiteDbSession session,
    ReplPreferences prefs,
    DotHelpCatalog help,
    string line)
{
    var parts = SplitDot(line);
    var cmd = parts.Command;
    var arg = parts.Argument;

    switch (cmd)
    {
        case ".help":
            ReplChrome.ShowHelp(console, help, arg);
            return ExitCodes.Ok;
        case ".collections":
        {
            var infos = session.ListCollectionInfos();
            var table = new Table().Border(TableBorder.Rounded).AddColumn("collection").AddColumn("documents");
            foreach (var info in infos)
            {
                table.AddRow(
                    Markup.Escape(info.Name),
                    info.DocumentCount < 0 ? "[dim]?[/]" : info.DocumentCount.ToString());
            }

            if (infos.Count == 0)
            {
                ReplChrome.Info(console, "(no collections)");
            }
            else
            {
                console.Write(table);
                console.WriteLine();
            }

            return ExitCodes.Ok;
        }
        case ".indexes":
        {
            var raw = session.ListIndexes(arg);
            var shaped = ResultShaping.Shape(raw.Columns, raw.Rows, raw.RecordsAffected, "document", prefs, TimeSpan.Zero);
            ResultPrinter.Write(console, shaped, prefs.Mode, prefs);
            LastResult.Current = shaped;
            return ExitCodes.Ok;
        }
        case ".info":
        {
            var info = session.GetInfo();
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

            if (LastResult.Current is null)
            {
                ReplChrome.Error(console, "No result to export yet.");
                return ExitCodes.Failure;
            }

            try
            {
                ResultPrinter.Export(arg, LastResult.Current);
                ReplChrome.Ok(console, $"wrote {Path.GetFullPath(arg)}");
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
    public static TabularResult? Current { get; set; }
}
