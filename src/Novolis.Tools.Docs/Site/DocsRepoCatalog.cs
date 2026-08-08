using System.Text.Json;

namespace Novolis.Tools.Docs.Site;

/// <summary>Marketing metadata for a repository (tagline, blurb, GitHub topics).</summary>
public sealed class DocsRepoMeta
{
    /// <summary>Short tagline shown on banners and cards.</summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>One-line description for catalog cards.</summary>
    public string Blurb { get; init; } = string.Empty;

    /// <summary>GitHub repository description.</summary>
    public string Desc { get; init; } = string.Empty;

    /// <summary>GitHub topics / docs tags.</summary>
    public IReadOnlyList<string> Topics { get; init; } = [];
}

/// <summary>Loads <c>repo-catalog.json</c> produced by Upgrade-RepoMarketingReadmes.ps1.</summary>
public static class DocsRepoCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>Loads catalog entries keyed by repository name (e.g. <c>novolis-documents</c>).</summary>
    public static IReadOnlyDictionary<string, DocsRepoMeta> Load(string? catalogPath)
    {
        if (string.IsNullOrWhiteSpace(catalogPath) || !File.Exists(catalogPath))
        {
            return new Dictionary<string, DocsRepoMeta>(StringComparer.OrdinalIgnoreCase);
        }

        using var stream = File.OpenRead(catalogPath);
        var raw = JsonSerializer.Deserialize<Dictionary<string, CatalogEntry>>(stream, JsonOptions)
                  ?? new Dictionary<string, CatalogEntry>();

        var result = new Dictionary<string, DocsRepoMeta>(StringComparer.OrdinalIgnoreCase);
        foreach (var (repo, entry) in raw)
        {
            result[repo] = new DocsRepoMeta
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
        }

        return result;
    }

    /// <summary>Banner file stem under <c>assets/banners/</c> (<c>.github</c> → <c>github-org</c>).</summary>
    public static string BannerStem(string repo) =>
        string.Equals(repo, ".github", StringComparison.OrdinalIgnoreCase) ? "github-org" : repo;

    private sealed class CatalogEntry
    {
        public string? Tag { get; set; }
        public string? Blurb { get; set; }
        public string? Desc { get; set; }
        public string[]? Topics { get; set; }
    }
}
