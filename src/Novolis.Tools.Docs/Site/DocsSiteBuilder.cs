using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Novolis.Markup.Markdown;

namespace Novolis.Tools.Docs.Site;

/// <summary>Builds a static HTML documentation site from a multi-repo <c>docs/</c> corpus.</summary>
public static class DocsSiteBuilder
{
    /// <summary>Scans the corpus and writes <c>index.html</c>, per-page HTML, and optional assets.</summary>
    /// <returns>Number of documentation pages written.</returns>
    public static int Build(DocsSiteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var pages = DocsCorpusScanner.Scan(options.CorpusDirectory, options.Org, options.DefaultBranch);
        return Build(options, pages);
    }

    /// <summary>Writes the site for a pre-scanned page list.</summary>
    public static int Build(DocsSiteOptions options, IReadOnlyList<DocsSitePage> pages)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(pages);

        var output = Path.GetFullPath(options.OutputDirectory);
        if (Directory.Exists(output))
        {
            Directory.Delete(output, recursive: true);
        }

        Directory.CreateDirectory(output);
        Directory.CreateDirectory(Path.Combine(output, "docs"));
        Directory.CreateDirectory(Path.Combine(output, "assets"));

        CopyAssets(options, output);

        var byRepo = pages
            .GroupBy(static p => p.Repo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => g.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var page in pages)
        {
            var slugMap = pages
                .Where(p => string.Equals(p.Repo, page.Repo, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(static p => p.RelativePath, static p => p.Slug, StringComparer.OrdinalIgnoreCase);

            var markdown = RewriteMarkdownLinks(page.Markdown, page.RelativePath, slugMap);
            markdown = StripLeadingH1(markdown);
            var bodyHtml = MarkdownDocument.Parse(markdown).ToHtml();
            var article = $"""
                <article class="article">
                  <div class="article-kicker">{Html(page.Repo)} / {Html(page.Kind)} / {Html(page.RelativePath)}</div>
                  <h1>{Html(page.Title)}</h1>
                  <div class="article-actions">
                    <a class="btn-primary" href="../index.html#docs">Back to docs index</a>
                    <a class="btn-secondary" href="../index.html?repo={Uri.EscapeDataString(page.Repo)}#docs">More from {Html(page.Repo)}</a>
                    <a class="btn-secondary" href="{Html(page.SourceUrl)}">GitHub source</a>
                  </div>
                  <div class="markdown-body">
                    {bodyHtml}
                  </div>
                </article>
                """;
            var html = PageShell(page.Title, $"Novolis documentation ({page.Repo}): {page.Title}", article, options.Org, nested: true);
            File.WriteAllText(Path.Combine(output, "docs", page.Slug + ".html"), html, Utf8NoBom());
        }

        File.WriteAllText(Path.Combine(output, "index.html"), IndexHtml(options, pages, byRepo), Utf8NoBom());
        File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty, Utf8NoBom());
        return pages.Count;
    }

    private static void CopyAssets(DocsSiteOptions options, string output)
    {
        if (!string.IsNullOrWhiteSpace(options.AssetsDirectory) && Directory.Exists(options.AssetsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(options.AssetsDirectory))
            {
                var name = Path.GetFileName(file);
                File.Copy(file, Path.Combine(output, "assets", name), overwrite: true);
            }
        }

        if (string.IsNullOrWhiteSpace(options.BrandDirectory) || !Directory.Exists(options.BrandDirectory))
        {
            return;
        }

        var brandOut = Path.Combine(output, "assets", "brand");
        Directory.CreateDirectory(brandOut);
        foreach (var name in new[] { "favicon.svg", "logo-icon.svg", "logo-brand-transparent.svg" })
        {
            var src = Path.Combine(options.BrandDirectory, name);
            if (File.Exists(src))
            {
                File.Copy(src, Path.Combine(brandOut, name), overwrite: true);
            }
        }

        var social = Path.Combine(options.BrandDirectory, "generated", "logo-social.png");
        if (File.Exists(social))
        {
            File.Copy(social, Path.Combine(brandOut, "logo-social.png"), overwrite: true);
        }

        var banners = Path.Combine(options.BrandDirectory, "banners");
        if (!Directory.Exists(banners))
        {
            return;
        }

        var bannersOut = Path.Combine(output, "assets", "banners");
        Directory.CreateDirectory(bannersOut);
        foreach (var file in Directory.EnumerateFiles(banners, "*.svg"))
        {
            File.Copy(file, Path.Combine(bannersOut, Path.GetFileName(file)), overwrite: true);
        }
    }

    private static string IndexHtml(DocsSiteOptions options, IReadOnlyList<DocsSitePage> pages, IReadOnlyDictionary<string, int> byRepo)
    {
        var generatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";
        var repoOptions = new StringBuilder();
        repoOptions.AppendLine("""<option value="all">All repositories</option>""");
        foreach (var repo in byRepo.Keys.OrderBy(static r => r, StringComparer.OrdinalIgnoreCase))
        {
            repoOptions.AppendLine($"""<option value="{Html(repo)}">{Html(repo)} ({byRepo[repo]})</option>""");
        }

        var docCards = new StringBuilder();
        foreach (var page in pages.OrderBy(static p => p.Repo, StringComparer.OrdinalIgnoreCase).ThenBy(static p => p.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            docCards.AppendLine($"""
                <article class="doc-card" data-doc-group="{Html(page.Repo)}" data-doc-kind="{Html(page.Kind)}" data-search="{Html(($"{page.Title} {page.Repo} {page.Kind} {page.RelativePath}").ToLowerInvariant())}">
                  <div class="doc-card-tags">
                    <span>{Html(page.Repo)}</span>
                    <span>{Html(page.Kind)}</span>
                  </div>
                  <h3><a href="docs/{Html(page.Slug)}.html">{Html(page.Title)}</a></h3>
                  <p>{Html($"{page.Repo}/{page.RelativePath}")}</p>
                  <div class="card-actions">
                    <a class="btn-primary" href="docs/{Html(page.Slug)}.html">Open docs page</a>
                    <a class="btn-secondary" href="{Html(page.SourceUrl)}">GitHub source</a>
                  </div>
                </article>
                """);
        }

        var repoCards = new StringBuilder();
        foreach (var repo in byRepo.Keys.OrderBy(static r => r, StringComparer.OrdinalIgnoreCase))
        {
            var count = byRepo[repo];
            var banner = File.Exists(Path.Combine(options.OutputDirectory, "assets", "banners", repo + ".svg"))
                ? $"""<img class="repo-banner" src="assets/banners/{Html(repo)}.svg" alt=""/>"""
                : $"""<div class="repo-banner text-banner">{Html(repo)}</div>""";
            repoCards.AppendLine($"""
                <article class="repo-card" data-kind="Libraries" data-search="{Html(repo.ToLowerInvariant())}">
                  {banner}
                  <div class="repo-card-body">
                    <div class="repo-meta">
                      <span>Library docs</span>
                      <a class="docs-count" href="index.html?repo={Uri.EscapeDataString(repo)}#docs">{count} docs</a>
                    </div>
                    <h3><a href="index.html?repo={Uri.EscapeDataString(repo)}#docs">{Html(repo)}</a></h3>
                    <p>Documentation sparse-checked out from <code>docs/</code> in {Html(repo)}.</p>
                    <div class="card-actions">
                      <a class="btn-primary" href="index.html?repo={Uri.EscapeDataString(repo)}#docs">Open docs</a>
                      <a class="btn-secondary" href="https://github.com/{Html(options.Org)}/{Html(repo)}">GitHub</a>
                    </div>
                  </div>
                </article>
                """);
        }

        var baseUrl = options.BaseUrl ?? $"https://{options.Org.ToLowerInvariant()}.github.io/.github/";
        return $$"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8"/>
              <meta name="viewport" content="width=device-width, initial-scale=1"/>
              <meta name="description" content="Novolis documentation generated from every public repository docs/ folder."/>
              <title>Novolis Docs</title>
              <link rel="icon" href="assets/brand/favicon.svg"/>
              <link rel="stylesheet" href="assets/site.css"/>
            </head>
            <body>
              <header class="topbar">
                <a class="brand" href="index.html" aria-label="Novolis docs home">
                  <img src="assets/brand/logo-icon.svg" alt=""/>
                  <span>Novolis Docs</span>
                </a>
                <nav>
                  <a href="#docs">Docs</a>
                  <a href="#portfolio">Repositories</a>
                  <a href="https://github.com/{{Html(options.Org)}}">GitHub</a>
                </nav>
              </header>
              <main>
                <section class="hero">
                  <div class="hero-grid" aria-hidden="true"></div>
                  <div class="hero-content">
                    <img class="hero-logo" src="assets/brand/logo-brand-transparent.svg" alt="Novolis"/>
                    <p class="eyebrow">Sparse-checkout docs corpus</p>
                    <h1>Documentation from every public Novolis repository.</h1>
                    <p class="hero-copy">Each library's <code>docs/</code> tree is sparse-checked out and rendered by <code>novolis-docs site</code> using Novolis.Markup.</p>
                    <div class="hero-actions">
                      <a href="#docs">Browse docs</a>
                      <a href="#portfolio">Repositories</a>
                    </div>
                  </div>
                  <div class="telemetry-panel" aria-label="Docs telemetry">
                    <div><span>{{byRepo.Count}}</span><strong>repositories</strong></div>
                    <div><span>{{pages.Count}}</span><strong>docs pages</strong></div>
                    <div><span>docs/</span><strong>sparse corpus</strong></div>
                    <div><span>novolis-docs</span><strong>site builder</strong></div>
                  </div>
                </section>

                <section id="docs" class="section docs-section">
                  <div class="section-heading">
                    <p class="eyebrow">Generated HTML from sparse-checked-out docs/</p>
                    <h2>Docs</h2>
                  </div>
                  <div class="controls docs-controls">
                    <label class="search-box">
                      <span>Search</span>
                      <input type="search" id="docSearch" placeholder="governance, raylib, nuget"/>
                    </label>
                    <label class="search-box">
                      <span>Repository</span>
                      <select id="docRepoFilter">
                        {{repoOptions}}
                      </select>
                    </label>
                    <label class="search-box">
                      <span>Kind</span>
                      <select id="docKindFilter">
                        <option value="all">All kinds</option>
                        <option value="Docs">Docs</option>
                      </select>
                    </label>
                  </div>
                  <p class="docs-hint">Each card opens a rendered docs page on this site. GitHub source is optional.</p>
                  <div class="doc-grid" id="docGrid">
                    {{docCards}}
                  </div>
                </section>

                <section id="portfolio" class="section">
                  <div class="section-heading">
                    <p class="eyebrow">Repositories contributing docs/</p>
                    <h2>Repositories</h2>
                  </div>
                  <div class="controls">
                    <label class="search-box">
                      <span>Search</span>
                      <input type="search" id="portfolioSearch" placeholder="raylib, audio, governance"/>
                    </label>
                    <div class="segmented" role="tablist" aria-label="Portfolio filters">
                      <button class="active" data-kind="all">All</button>
                    </div>
                  </div>
                  <div class="repo-grid" id="repoGrid">
                    {{repoCards}}
                  </div>
                </section>
              </main>
              <footer class="footer">
                <span>Generated {{generatedAt}}</span>
                <a href="{{Html(baseUrl)}}">{{Html(baseUrl)}}</a>
                <a href="https://github.com/{{Html(options.Org)}}/.github">Source</a>
              </footer>
              <script src="assets/site.js"></script>
            </body>
            </html>
            """;
    }

    private static string PageShell(string title, string description, string body, string org, bool nested)
    {
        var prefix = nested ? "../" : string.Empty;
        return $"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8"/>
              <meta name="viewport" content="width=device-width, initial-scale=1"/>
              <meta name="description" content="{Html(description)}"/>
              <title>{Html(title)} - Novolis Docs</title>
              <link rel="icon" href="{prefix}assets/brand/favicon.svg"/>
              <link rel="stylesheet" href="{prefix}assets/site.css"/>
            </head>
            <body>
              <header class="topbar">
                <a class="brand" href="{prefix}index.html" aria-label="Novolis docs home">
                  <img src="{prefix}assets/brand/logo-icon.svg" alt=""/>
                  <span>Novolis Docs</span>
                </a>
                <nav>
                  <a href="{prefix}index.html#docs">Docs</a>
                  <a href="{prefix}index.html#portfolio">Repositories</a>
                  <a href="https://github.com/{Html(org)}">GitHub</a>
                </nav>
              </header>
              <main class="article-shell">
                {body}
              </main>
              <footer class="footer">
                <a href="{prefix}index.html#docs">Docs index</a>
                <a href="https://github.com/{Html(org)}/.github">Source</a>
              </footer>
            </body>
            </html>
            """;
    }

    private static string RewriteMarkdownLinks(string markdown, string currentRelative, IReadOnlyDictionary<string, string> slugByPath)
    {
        return Regex.Replace(markdown, @"\[([^\]]+)\]\(([^)]+)\)", match =>
        {
            var label = match.Groups[1].Value;
            var href = match.Groups[2].Value;
            if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith('#'))
            {
                return match.Value;
            }

            var pathPart = href.Split('#', 2)[0];
            var fragment = href.Contains('#', StringComparison.Ordinal) ? "#" + href.Split('#', 2)[1] : string.Empty;
            if (string.IsNullOrWhiteSpace(pathPart) || !pathPart.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            var resolved = ResolveRelative(currentRelative, pathPart);
            if (slugByPath.TryGetValue(resolved, out var slug))
            {
                return $"[{label}]({slug}.html{fragment})";
            }

            return match.Value;
        });
    }

    private static string ResolveRelative(string currentRelative, string href)
    {
        var stack = new List<string>();
        var currentParts = currentRelative.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (currentParts.Length > 1)
        {
            stack.AddRange(currentParts.Take(currentParts.Length - 1));
        }

        foreach (var part in href.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part is ".")
            {
                continue;
            }

            if (part is "..")
            {
                if (stack.Count > 0)
                {
                    stack.RemoveAt(stack.Count - 1);
                }

                continue;
            }

            stack.Add(part);
        }

        return string.Join('/', stack);
    }

    private static string StripLeadingH1(string markdown)
    {
        var match = Regex.Match(markdown, @"(?m)^\s*#\s+.+\r?\n+");
        return match.Success ? markdown.Remove(match.Index, match.Length) : markdown;
    }

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static Encoding Utf8NoBom() => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}
