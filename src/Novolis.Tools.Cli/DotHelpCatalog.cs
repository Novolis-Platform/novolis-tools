namespace Novolis.Tools.Cli;

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
