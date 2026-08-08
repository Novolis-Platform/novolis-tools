using System.Net;
using System.Text;

namespace Novolis.Tools.Docs.Site;

/// <summary>Builds sidebar navigation HTML from a repository's docs/ layout.</summary>
public static class DocsNavBuilder
{
    /// <summary>Renders a sidebar for one repository, highlighting <paramref name="current"/>.</summary>
    public static string BuildSidebar(string repo, IReadOnlyList<DocsSitePage> repoPages, DocsSitePage current)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);
        ArgumentNullException.ThrowIfNull(repoPages);
        ArgumentNullException.ThrowIfNull(current);

        var byDocsPath = repoPages.ToDictionary(static p => p.DocsRelativePath, StringComparer.OrdinalIgnoreCase);
        var overviewHref = ToRepoLocalHref($"{repo}/index.html", current.OutputRelativePath);
        var librariesHref = ToRepoLocalHref("index.html", current.OutputRelativePath);

        var sb = new StringBuilder();
        sb.AppendLine("""<nav class="docs-nav" aria-label="Documentation">""");
        sb.AppendLine($"""<div class="docs-nav-repo"><a href="{Html(overviewHref)}">{Html(repo)}</a></div>""");

        sb.AppendLine("""<div class="docs-nav-section"><h2>Required</h2><ul>""");
        foreach (var required in DocsCorpusScanner.RequiredDocNames)
        {
            if (string.Equals(required, "README.md", StringComparison.OrdinalIgnoreCase))
            {
                var landing = repoPages.First(static p => p.IsLanding);
                sb.AppendLine(NavItem(landing, current, label: "Overview"));
                continue;
            }

            if (byDocsPath.TryGetValue(required, out var page))
            {
                sb.AppendLine(NavItem(page, current));
            }
            else
            {
                var label = Path.GetFileNameWithoutExtension(required).Replace('-', ' ');
                sb.AppendLine($"""<li class="missing"><span>{Html(label)}</span></li>""");
            }
        }

        sb.AppendLine("</ul></div>");

        sb.AppendLine("""<div class="docs-nav-section"><h2>Browse</h2>""");
        sb.AppendLine(BuildTree(repoPages.Where(static p => !p.IsLanding).ToList(), current));
        sb.AppendLine("</div>");

        sb.AppendLine($"""
            <div class="docs-nav-actions">
              <a class="btn-secondary" href="{Html(librariesHref)}">All libraries</a>
              <a class="btn-secondary" href="{Html(current.SourceUrl)}">GitHub source</a>
            </div>
            """);
        sb.AppendLine("</nav>");
        return sb.ToString();
    }

    /// <summary>Returns previous/next pages in browse order (landing first, then tree order).</summary>
    public static (DocsSitePage? Previous, DocsSitePage? Next) Adjacent(IReadOnlyList<DocsSitePage> repoPages, DocsSitePage current)
    {
        var ordered = OrderForBrowse(repoPages).ToList();
        var index = ordered.FindIndex(p =>
            string.Equals(p.OutputRelativePath, current.OutputRelativePath, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return (null, null);
        }

        return (
            index > 0 ? ordered[index - 1] : null,
            index < ordered.Count - 1 ? ordered[index + 1] : null);
    }

    /// <summary>Browse order: landing, required guides, then remaining paths.</summary>
    public static IEnumerable<DocsSitePage> OrderForBrowse(IReadOnlyList<DocsSitePage> repoPages)
    {
        var remaining = repoPages.ToList();
        var landing = remaining.FirstOrDefault(static p => p.IsLanding);
        if (landing is not null)
        {
            remaining.Remove(landing);
            yield return landing;
        }

        foreach (var required in DocsCorpusScanner.RequiredDocNames.Skip(1))
        {
            var page = remaining.FirstOrDefault(p =>
                string.Equals(p.DocsRelativePath, required, StringComparison.OrdinalIgnoreCase));
            if (page is null)
            {
                continue;
            }

            remaining.Remove(page);
            yield return page;
        }

        foreach (var page in remaining.OrderBy(static p => p.DocsRelativePath, StringComparer.OrdinalIgnoreCase))
        {
            yield return page;
        }
    }

    /// <summary>Computes a relative href from one site page to another.</summary>
    public static string ToRepoLocalHref(string targetOutputRelative, string currentOutputRelative)
    {
        var target = targetOutputRelative.Replace('\\', '/').TrimStart('/');
        var current = currentOutputRelative.Replace('\\', '/').TrimStart('/');
        var currentDir = Path.GetDirectoryName(current)?.Replace('\\', '/') ?? string.Empty;

        // Anchor both paths under a fake root so GetRelativePath does not depend on process cwd.
        var root = Path.Combine(Path.GetTempPath(), "novolis-docs-href-root");
        var fromDir = string.IsNullOrEmpty(currentDir) ? root : Path.Combine(root, currentDir.Replace('/', Path.DirectorySeparatorChar));
        var toFile = Path.Combine(root, target.Replace('/', Path.DirectorySeparatorChar));
        var relative = Path.GetRelativePath(fromDir, toFile).Replace('\\', '/');
        if (relative is ".")
        {
            return "./" + Path.GetFileName(target);
        }

        return relative.StartsWith('.') ? relative : "./" + relative;
    }

    private static string BuildTree(IReadOnlyList<DocsSitePage> pages, DocsSitePage current)
    {
        if (pages.Count == 0)
        {
            return "<p class=\"quiet\">No additional pages.</p>";
        }

        var sb = new StringBuilder();
        sb.AppendLine("<ul class=\"docs-tree\">");

        foreach (var page in pages
                     .Where(static p => !p.DocsRelativePath.Contains('/', StringComparison.Ordinal))
                     .OrderBy(static p => p.DocsRelativePath, StringComparer.OrdinalIgnoreCase))
        {
            // Required guides already listed above — still show in browse for completeness except exact required names
            if (DocsCorpusScanner.RequiredDocNames.Any(r =>
                    string.Equals(r, page.DocsRelativePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            sb.AppendLine(NavItem(page, current));
        }

        var topFolders = pages
            .Select(p =>
            {
                var slash = p.DocsRelativePath.IndexOf('/');
                return slash < 0 ? null : p.DocsRelativePath[..slash];
            })
            .Where(static d => d is not null)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(static d => d, StringComparer.OrdinalIgnoreCase);

        foreach (var folder in topFolders)
        {
            sb.AppendLine($"""<li class="folder"><span>{Html(folder)}</span><ul>""");
            foreach (var page in pages
                         .Where(p => p.DocsRelativePath.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase))
                         .OrderBy(static p => p.DocsRelativePath, StringComparer.OrdinalIgnoreCase))
            {
                var label = page.DocsRelativePath[(folder.Length + 1)..];
                if (label.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    label = label[..^3];
                }

                sb.AppendLine(NavItem(page, current, label: label.Replace("/", " / ")));
            }

            sb.AppendLine("</ul></li>");
        }

        sb.AppendLine("</ul>");
        return sb.ToString();
    }

    private static string NavItem(DocsSitePage page, DocsSitePage current, string? label = null)
    {
        var text = label ?? page.Title;
        var href = ToRepoLocalHref(page.OutputRelativePath, current.OutputRelativePath);
        var active = string.Equals(page.OutputRelativePath, current.OutputRelativePath, StringComparison.OrdinalIgnoreCase)
            ? " class=\"active\""
            : string.Empty;
        return $"<li{active}><a href=\"{Html(href)}\">{Html(text)}</a></li>";
    }

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);
}
