using System.Text.RegularExpressions;

namespace Novolis.Tools.Docs.Site;

/// <summary>Discovers Markdown under each repo's <c>docs/</c> folder in a sparse-checkout corpus.</summary>
public static class DocsCorpusScanner
{
    private static readonly Regex HeadingRegex = new(@"^\s*#\s+(.+)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Scans <paramref name="corpusDirectory"/> for <c>{repo}/docs/**/*.md</c>.</summary>
    public static IReadOnlyList<DocsSitePage> Scan(string corpusDirectory, string org, string defaultBranch = "main")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(corpusDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(org);
        if (!Directory.Exists(corpusDirectory))
        {
            throw new DirectoryNotFoundException($"Corpus directory not found: {corpusDirectory}");
        }

        var pages = new List<DocsSitePage>();
        foreach (var repoDir in Directory.EnumerateDirectories(corpusDirectory).OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
        {
            var repo = Path.GetFileName(repoDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrWhiteSpace(repo) || repo is "." or "..")
            {
                continue;
            }

            var docsRoot = Path.Combine(repoDir, "docs");
            if (!Directory.Exists(docsRoot))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories).OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
            {
                var relative = Path.GetRelativePath(repoDir, file).Replace('\\', '/');
                if (relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                    || relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
                    || relative.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var markdown = File.ReadAllText(file);
                if (markdown.Length > 500_000)
                {
                    continue;
                }

                var title = Path.GetFileNameWithoutExtension(file);
                var heading = HeadingRegex.Match(markdown);
                if (heading.Success)
                {
                    title = heading.Groups[1].Value.Trim();
                }

                var slug = Slugify($"{repo}/{relative[..^3]}");
                pages.Add(new DocsSitePage
                {
                    Repo = repo,
                    RelativePath = relative,
                    Title = title,
                    Slug = slug,
                    Kind = "Docs",
                    Markdown = markdown,
                    SourceUrl = $"https://github.com/{org}/{repo}/blob/{defaultBranch}/{relative}",
                });
            }
        }

        return pages;
    }

    /// <summary>Slugifies a path-like value for HTML filenames.</summary>
    public static string Slugify(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var slug = Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "item" : slug;
    }
}
