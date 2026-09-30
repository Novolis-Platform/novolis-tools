using System.Text.Json;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Marketing;

/// <summary>Built-in repository marketing catalog (source of truth for novolis-docs marketing).</summary>
public static class PlatformRepoCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private static readonly Lazy<IReadOnlyDictionary<string, DocsRepoMeta>> LazyEntries = new(LoadEmbedded);

    /// <summary>All catalog entries keyed by repository folder name.</summary>
    public static IReadOnlyDictionary<string, DocsRepoMeta> Entries => LazyEntries.Value;

    /// <summary>Looks up metadata or returns a generic fallback for unknown repos.</summary>
    public static DocsRepoMeta GetOrDefault(string repoName)
    {
        if (Entries.TryGetValue(repoName, out var meta))
        {
            return meta;
        }

        return new DocsRepoMeta
        {
            Tag = "Novolis ecosystem library",
            Blurb = $"Part of the Novolis platform ({repoName}).",
            Desc = $"Novolis ecosystem repository: {repoName}",
            Topics = ["dotnet", "novolis"],
        };
    }

    /// <summary>Loads catalog JSON from disk (e.g. seeded <c>repo-catalog.json</c>) for docs pack seeding.</summary>
    public static IReadOnlyDictionary<string, DocsRepoMeta> LoadFromFile(string path) =>
        DocsRepoCatalog.Load(path);

    private static IReadOnlyDictionary<string, DocsRepoMeta> LoadEmbedded()
    {
        var assembly = typeof(PlatformRepoCatalog).Assembly;
        const string resourceName = "Novolis.Tools.Docs.Marketing.platform-repo-catalog.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}.");
        var raw = JsonSerializer.Deserialize<Dictionary<string, CatalogEntry>>(stream, JsonOptions)
                  ?? new Dictionary<string, CatalogEntry>();

        var result = new Dictionary<string, DocsRepoMeta>(StringComparer.OrdinalIgnoreCase);
        foreach (var (repo, entry) in raw)
        {
            result[repo] = ToMeta(entry);
        }

        return result;
    }

    internal static DocsRepoMeta ToMeta(CatalogEntry entry) =>
        new()
        {
            Tag = entry.Tag?.Trim() ?? string.Empty,
            Blurb = entry.Blurb?.Trim() ?? string.Empty,
            Desc = entry.Desc?.Trim() ?? string.Empty,
            Topics = (entry.Topics ?? [])
                .Where(static t => !string.IsNullOrWhiteSpace(t))
                .Select(static t => t.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
        };

    internal sealed class CatalogEntry
    {
        public string? Tag { get; set; }
        public string? Blurb { get; set; }
        public string? Desc { get; set; }
        public string[]? Topics { get; set; }
    }
}
