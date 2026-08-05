using Spectre.Console;

namespace Novolis.Tools.Cli;

/// <summary>Spectre chrome for interactive REPLs.</summary>
public static class ReplChrome
{
    /// <summary>Writes a branded open banner.</summary>
    public static void Banner(IAnsiConsole console, string toolName, string databaseLabel, ReplPreferences prefs, string tip)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(prefs);

        var grid = new Grid();
        grid.AddColumn();
        grid.AddRow(new Markup($"[bold aqua]{Markup.Escape(toolName)}[/]"));
        grid.AddRow(new Markup($"Database: [white]{Markup.Escape(databaseLabel)}[/]"));
        grid.AddRow(new Markup(
            $"Mode: [yellow]{OutputModes.Format(prefs.Mode)}[/]  " +
            $"Limit: [yellow]{(prefs.Limit == 0 ? "unlimited" : prefs.Limit.ToString())}[/]  " +
            $"Read-only: {(prefs.ReadOnly ? "[green]yes[/]" : "[grey]no[/]")}  " +
            $"Confirm: {(prefs.ConfirmDestructive ? "[green]on[/]" : "[grey]off[/]")}"));
        grid.AddRow(new Markup($"[dim]{Markup.Escape(tip)}[/]"));

        console.Write(new Panel(grid)
            .Header("[bold]session[/]")
            .BorderColor(Color.Teal)
            .Padding(1, 0));
        console.WriteLine();
    }

    /// <summary>Shows a help panel from a catalog.</summary>
    public static void ShowHelp(IAnsiConsole console, DotHelpCatalog catalog, string? topic = null)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(catalog);

        if (!string.IsNullOrWhiteSpace(topic))
        {
            var key = topic.Trim().StartsWith('.') ? topic.Trim() : "." + topic.Trim();
            if (catalog.TryGet(key, out var entry))
            {
                console.Write(new Panel(
                        new Markup(
                            $"[bold]{Markup.Escape(entry.Synopsis)}[/]\n\n" +
                            $"{Markup.Escape(entry.Details)}\n\n" +
                            $"[dim]Example:[/] [aqua]{Markup.Escape(entry.Example)}[/]"))
                    .Header($"[bold]{Markup.Escape(entry.Name)}[/]")
                    .BorderColor(Color.Aqua));
                return;
            }

            console.MarkupLine($"[yellow]Unknown help topic[/] [white]{Markup.Escape(topic)}[/]. Try [aqua].help[/].");
            Suggest(console, catalog.Names, key);
            return;
        }

        var table = new Table()
            .Border(TableBorder.Simple)
            .HideHeaders()
            .AddColumn("cmd")
            .AddColumn("desc");

        foreach (var entry in catalog.Entries)
        {
            table.AddRow($"[aqua]{Markup.Escape(entry.Name)}[/]", Markup.Escape(entry.Synopsis));
        }

        console.Write(new Panel(table)
            .Header("[bold].help[/] — dot-commands (add a name for details)")
            .BorderColor(Color.Aqua));
        console.MarkupLine("[dim]Tip: multi-line SQL ends with ; — Ctrl+C cancels a line — .quit leaves.[/]");
    }

    /// <summary>Prints an error with Spectre markup.</summary>
    public static void Error(IAnsiConsole console, string message)
    {
        console.MarkupLine($"[red]error:[/] {Markup.Escape(message)}");
    }

    /// <summary>Prints a warning.</summary>
    public static void Warn(IAnsiConsole console, string message)
    {
        console.MarkupLine($"[yellow]warn:[/] {Markup.Escape(message)}");
    }

    /// <summary>Prints an info line.</summary>
    public static void Info(IAnsiConsole console, string message)
    {
        console.MarkupLine($"[grey]{Markup.Escape(message)}[/]");
    }

    /// <summary>Prints a success line.</summary>
    public static void Ok(IAnsiConsole console, string message)
    {
        console.MarkupLine($"[green]{Markup.Escape(message)}[/]");
    }

    /// <summary>Suggests close matches for an unknown command.</summary>
    public static void Suggest(IAnsiConsole console, IEnumerable<string> candidates, string input)
    {
        var matches = candidates
            .Select(c => (Name: c, Distance: Levenshtein(c, input)))
            .Where(x => x.Distance <= 3)
            .OrderBy(x => x.Distance)
            .Take(3)
            .Select(x => x.Name)
            .ToArray();
        if (matches.Length > 0)
        {
            console.MarkupLine($"[dim]Did you mean[/] {string.Join(", ", matches.Select(m => $"[aqua]{Markup.Escape(m)}[/]"))}[dim]?[/]");
        }
    }

    /// <summary>Reads a line with a colored prompt; returns null on EOF.</summary>
    public static string? ReadLine(IAnsiConsole console, string prompt)
    {
        console.Markup($"[bold teal]{Markup.Escape(prompt)}[/] ");
        return Console.ReadLine();
    }

    /// <summary>
    /// Reads one statement: continues until a line ending with <c>;</c> (for SQL-like input)
    /// or a single line for dot-commands.
    /// </summary>
    public static string? ReadStatement(IAnsiConsole console, string prompt, string continuePrompt = "   ...> ")
    {
        var first = ReadLine(console, prompt);
        if (first is null)
        {
            return null;
        }

        if (first.StartsWith('.') || first.TrimEnd().EndsWith(';') || !LooksLikeSql(first))
        {
            return first.TrimEnd();
        }

        var sb = new System.Text.StringBuilder(first);
        while (!cancellationRequested())
        {
            if (sb.ToString().TrimEnd().EndsWith(';'))
            {
                break;
            }

            var next = ReadLine(console, continuePrompt);
            if (next is null)
            {
                break;
            }

            sb.AppendLine();
            sb.Append(next);
        }

        return sb.ToString().TrimEnd();

        static bool cancellationRequested() => false;
    }

    /// <summary>Confirms a destructive action when preferred.</summary>
    public static bool ConfirmDestructive(IAnsiConsole console, ReplPreferences prefs, string summary)
    {
        if (!prefs.ConfirmDestructive || prefs.ReadOnly)
        {
            return !prefs.ReadOnly;
        }

        return console.Confirm($"[yellow]Destructive:[/] {Markup.Escape(summary)} — continue?", defaultValue: false);
    }

    /// <summary>Heuristic: statement looks like SQL worth multi-line collection.</summary>
    public static bool LooksLikeSql(string line)
    {
        var t = line.TrimStart();
        if (t.Length == 0 || t.StartsWith('.'))
        {
            return false;
        }

        return t.StartsWith("select", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("with", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("insert", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("update", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("delete", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("create", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("drop", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("alter", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("pragma", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("begin", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("commit", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("rollback", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("explain", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("vacuum", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("analyze", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("replace", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Heuristic: destructive SQL / shell.</summary>
    public static bool IsDestructiveSql(string sql)
    {
        var t = sql.TrimStart();
        return t.StartsWith("drop", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("delete", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("truncate", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("alter", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("vacuum", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Heuristic: write SQL blocked in read-only sessions.</summary>
    public static bool IsWriteSql(string sql)
    {
        var t = sql.TrimStart();
        if (t.StartsWith("select", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("with", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("pragma", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("explain", StringComparison.OrdinalIgnoreCase)
            || t.StartsWith("show", StringComparison.OrdinalIgnoreCase))
        {
            // PRAGMA can still write (journal_mode=WAL etc.) — treat assignment as write.
            if (t.StartsWith("pragma", StringComparison.OrdinalIgnoreCase) && t.Contains('=', StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        return true;
    }

    private static int Levenshtein(string a, string b)
    {
        var n = a.Length;
        var m = b.Length;
        var d = new int[n + 1, m + 1];
        for (var i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
