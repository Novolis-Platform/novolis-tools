using System.Text;
using System.Text.RegularExpressions;

namespace Novolis.Tools.Docs.Site;

/// <summary>Discovers Markdown under each repo's <c>docs/</c> folder in a sparse-checkout corpus.</summary>
public static class DocsCorpusScanner
{
    private static readonly Regex HeadingRegex = new(@"^\s*#\s+(.+)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>Standard Novolis docs entry filenames (order = required-nav priority).</summary>
    public static readonly string[] RequiredDocNames =
    [
        "README.md",
        "getting-started.md",
        "design.md",
        "release.md",
    ];

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

            var repoPages = new List<DocsSitePage>();
            foreach (var file in Directory.EnumerateFiles(docsRoot, "*.md", SearchOption.AllDirectories)
                         .OrderBy(static p => p, StringComparer.OrdinalIgnoreCase))
            {
                var relative = Path.GetRelativePath(repoDir, file).Replace('\\', '/');
                if (relative.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
                    || relative.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
                    || relative.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var docsRelative = Path.GetRelativePath(docsRoot, file).Replace('\\', '/');
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

                var isReadme = string.Equals(docsRelative, "README.md", StringComparison.OrdinalIgnoreCase);
                repoPages.Add(new DocsSitePage
                {
                    Repo = repo,
                    RelativePath = relative,
                    DocsRelativePath = docsRelative,
                    OutputRelativePath = ToOutputPath(repo, docsRelative),
                    Title = isReadme ? $"{repo} docs" : title,
                    Markdown = markdown,
                    SourceUrl = $"https://github.com/{org}/{repo}/blob/{defaultBranch}/{relative}",
                    IsLanding = isReadme,
                });
            }

            if (repoPages.Count == 0)
            {
                continue;
            }

            if (!repoPages.Any(static p => p.IsLanding))
            {
                repoPages.Insert(0, CreateGeneratedLanding(repo, org, defaultBranch, repoPages));
            }

            pages.AddRange(repoPages);
        }

        return pages;
    }

    /// <summary>Maps a docs-relative Markdown path to a site output path under the repo folder.</summary>
    public static string ToOutputPath(string repo, string docsRelativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);
        ArgumentException.ThrowIfNullOrWhiteSpace(docsRelativePath);
        var normalized = docsRelativePath.Replace('\\', '/').TrimStart('/');
        if (string.Equals(normalized, "README.md", StringComparison.OrdinalIgnoreCase))
        {
            return $"{repo}/index.html";
        }

        if (normalized.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^3] + ".html";
        }

        return $"{repo}/{normalized}";
    }

    private static DocsSitePage CreateGeneratedLanding(
        string repo,
        string org,
        string branch,
        IReadOnlyList<DocsSitePage> pages)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {repo} documentation");
        sb.AppendLine();
        sb.AppendLine($"Documentation for [`{repo}`](https://github.com/{org}/{repo}). This overview was generated because `docs/README.md` is not present yet.");
        sb.AppendLine();
        sb.AppendLine("## Required guides");
        sb.AppendLine();
        foreach (var required in RequiredDocNames.Skip(1))
        {
            var page = pages.FirstOrDefault(p =>
                string.Equals(p.DocsRelativePath, required, StringComparison.OrdinalIgnoreCase));
            if (page is null)
            {
                sb.AppendLine($"- {Path.GetFileNameWithoutExtension(required)} — *missing*");
            }
            else
            {
                sb.AppendLine($"- [{page.Title}]({page.DocsRelativePath})");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## All pages");
        sb.AppendLine();
        foreach (var page in pages.OrderBy(static p => p.DocsRelativePath, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- [{page.Title}]({page.DocsRelativePath})");
        }

        return new DocsSitePage
        {
            Repo = repo,
            RelativePath = "docs/README.md",
            DocsRelativePath = "README.md",
            OutputRelativePath = $"{repo}/index.html",
            Title = $"{repo} docs",
            Markdown = sb.ToString(),
            SourceUrl = $"https://github.com/{org}/{repo}/tree/{branch}/docs",
            IsLanding = true,
            IsGeneratedLanding = true,
        };
    }
}
