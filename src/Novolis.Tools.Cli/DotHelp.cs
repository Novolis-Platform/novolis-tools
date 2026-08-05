namespace Novolis.Tools.Cli;

/// <summary>One dot-command help entry.</summary>
/// <param name="Name">Command name including leading dot.</param>
/// <param name="Synopsis">One-line summary.</param>
/// <param name="Details">Longer description.</param>
/// <param name="Example">Example invocation.</param>
public sealed record DotHelpEntry(string Name, string Synopsis, string Details, string Example);

/// <summary>Catalog of `.help` topics for a REPL.</summary>
public sealed class DotHelpCatalog
{
    private readonly Dictionary<string, DotHelpEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Ordered entries.</summary>
    public IReadOnlyList<DotHelpEntry> Entries => _entries.Values.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    /// <summary>Command names.</summary>
    public IEnumerable<string> Names => _entries.Keys;

    /// <summary>Adds or replaces an entry.</summary>
    public DotHelpCatalog Add(string name, string synopsis, string details, string example)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var key = name.StartsWith('.') ? name : "." + name;
        _entries[key] = new DotHelpEntry(key, synopsis, details, example);
        return this;
    }

    /// <summary>Looks up a topic.</summary>
    public bool TryGet(string name, out DotHelpEntry entry) => _entries.TryGetValue(name, out entry!);
}

/// <summary>Pit-of-success open rules for file databases.</summary>
public static class OpenGuards
{
    /// <summary>
    /// Validates open intent: refuse to create a new file unless <paramref name="create"/>,
    /// refuse missing files unless create, and resolve <c>:memory:</c>.
    /// </summary>
    public static string ResolveDataSource(string database, bool create, bool readOnly, out string displayLabel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        if (string.Equals(database, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            if (readOnly)
            {
                throw new InvalidOperationException(":memory: cannot be opened read-only (nothing durable to inspect).");
            }

            displayLabel = ":memory:";
            return ":memory:";
        }

        if (readOnly && create)
        {
            throw new InvalidOperationException("--read-only and --create cannot be combined.");
        }

        // Connection-string form: extract the file path and still enforce create/read-only guards.
        if (database.Contains('=', StringComparison.Ordinal))
        {
            var filePath = TryExtractFilePath(database);
            if (filePath is not null
                && string.Equals(filePath, ":memory:", StringComparison.OrdinalIgnoreCase)
                && readOnly)
            {
                throw new InvalidOperationException(":memory: cannot be opened read-only (nothing durable to inspect).");
            }

            if (filePath is not null
                && !string.Equals(filePath, ":memory:", StringComparison.OrdinalIgnoreCase))
            {
                EnsureFileOpenAllowed(filePath, create, readOnly);
            }

            displayLabel = database;
            return database;
        }

        var full = Path.GetFullPath(database);
        displayLabel = full;
        EnsureFileOpenAllowed(full, create, readOnly);
        return full;
    }

    /// <summary>Extracts a file path from common SQLite / LiteDB connection-string shapes.</summary>
    public static string? TryExtractFilePath(string connectionString)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = part[..eq].Trim();
            var value = part[(eq + 1)..].Trim();
            if (key.Equals("Data Source", StringComparison.OrdinalIgnoreCase)
                || key.Equals("DataSource", StringComparison.OrdinalIgnoreCase)
                || key.Equals("Filename", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    private static void EnsureFileOpenAllowed(string path, bool create, bool readOnly)
    {
        var full = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
        var exists = File.Exists(full);

        if (readOnly && !exists)
        {
            throw new FileNotFoundException(
                $"Database not found (read-only open does not create files): {full}", full);
        }

        if (!exists && !create)
        {
            throw new FileNotFoundException(
                $"Database not found: {full}. Pass --create to create a new file (pit of success: refuse accidental empty DBs).",
                full);
        }
    }
}

/// <summary>Applies row limits and timing wrappers around tabular results.</summary>
public static class ResultShaping
{
    /// <summary>Truncates rows according to preferences and stamps elapsed time.</summary>
    public static TabularResult Shape(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyList<string>> rows,
        int? recordsAffected,
        string unit,
        ReplPreferences prefs,
        TimeSpan elapsed)
    {
        if (columns.Count == 0)
        {
            return TabularResult.Affected(recordsAffected ?? 0, unit, elapsed);
        }

        var limit = prefs.Limit;
        var truncated = limit > 0 && rows.Count > limit;
        var view = truncated ? rows.Take(limit).ToArray() : rows;
        return TabularResult.Grid(columns, view, unit, truncated, elapsed);
    }
}
