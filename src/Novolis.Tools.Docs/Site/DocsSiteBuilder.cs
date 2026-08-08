using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Novolis.Markup.Markdown;

namespace Novolis.Tools.Docs.Site;

/// <summary>Builds a multi-page documentation site from a multi-repo <c>docs/</c> corpus.</summary>
public static class DocsSiteBuilder
{
    /// <summary>Scans the corpus and writes the catalog plus per-repo doc sites.</summary>
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
        Directory.CreateDirectory(Path.Combine(output, "assets"));
        CopyAssets(options, output);

        var catalog = DocsRepoCatalog.Load(ResolveCatalogPath(options));
        var byRepo = pages
            .GroupBy(static p => p.Repo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static g => g.Key, static g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var (repo, repoPages) in byRepo.OrderBy(static kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            Directory.CreateDirectory(Path.Combine(output, repo));
            var slugMap = repoPages.ToDictionary(
                static p => p.DocsRelativePath,
                static p => p.OutputRelativePath,
                StringComparer.OrdinalIgnoreCase);

            catalog.TryGetValue(repo, out var meta);
            foreach (var page in repoPages)
            {
                WriteDocPage(options, output, page, repoPages, slugMap, meta);
            }
        }

        File.WriteAllText(Path.Combine(output, "index.html"), CatalogHtml(options, byRepo, catalog), Utf8NoBom());
        File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty, Utf8NoBom());
        return pages.Count;
    }

    private static void WriteDocPage(
        DocsSiteOptions options,
        string output,
        DocsSitePage page,
        IReadOnlyList<DocsSitePage> repoPages,
        IReadOnlyDictionary<string, string> slugMap,
        DocsRepoMeta? meta)
    {
        var markdown = RewriteMarkdownLinks(page.Markdown, page.DocsRelativePath, page.OutputRelativePath, slugMap);
        markdown = StripLeadingH1(markdown);
        var bodyHtml = RenderBodyHtml(markdown, page);
        var sidebar = DocsNavBuilder.BuildSidebar(page.Repo, repoPages, page);
        var (previous, next) = DocsNavBuilder.Adjacent(repoPages, page);

        var pager = new StringBuilder();
        pager.AppendLine("""<div class="docs-pager">""");
        if (previous is not null)
        {
            var href = DocsNavBuilder.ToRepoLocalHref(previous.OutputRelativePath, page.OutputRelativePath);
            pager.AppendLine($"""<a class="prev" href="{Html(href)}">← {Html(previous.Title)}</a>""");
        }
        else
        {
            pager.AppendLine("""<span></span>""");
        }

        if (next is not null)
        {
            var href = DocsNavBuilder.ToRepoLocalHref(next.OutputRelativePath, page.OutputRelativePath);
            pager.AppendLine($"""<a class="next" href="{Html(href)}">{Html(next.Title)} →</a>""");
        }
        else
        {
            pager.AppendLine("""<span></span>""");
        }

        pager.AppendLine("</div>");

        var kicker = page.IsGeneratedLanding
            ? $"{page.Repo} / generated overview"
            : $"{page.Repo} / {page.DocsRelativePath}";
        var tagline = meta is not null && !string.IsNullOrWhiteSpace(meta.Tag)
            ? $"""<p class="article-tagline">{Html(meta.Tag)}</p>"""
            : string.Empty;
        var topics = TopicsHtml(meta?.Topics);

        var article = $"""
            <article class="article">
              <div class="article-kicker">{Html(kicker)}</div>
              <h1>{Html(page.Title)}</h1>
              {tagline}
              {topics}
              <div class="markdown-body">
                {bodyHtml}
              </div>
              {pager}
            </article>
            """;

        var depth = page.OutputRelativePath.Count(static c => c == '/');
        var assetPrefix = string.Concat(Enumerable.Repeat("../", depth));
        var html = DocShell(page.Title, page.Repo, article, sidebar, options.Org, assetPrefix);
        var outFile = Path.Combine(output, page.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
        File.WriteAllText(outFile, html, Utf8NoBom());
    }

    private static string CatalogHtml(
        DocsSiteOptions options,
        IReadOnlyDictionary<string, List<DocsSitePage>> byRepo,
        IReadOnlyDictionary<string, DocsRepoMeta> catalog)
    {
        var generatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + " UTC";
        var output = Path.GetFullPath(options.OutputDirectory);
        var cards = new StringBuilder();
        foreach (var repo in byRepo.Keys.OrderBy(static r => r, StringComparer.OrdinalIgnoreCase))
        {
            var pages = byRepo[repo];
            var landing = pages.First(static p => p.IsLanding);
            var count = pages.Count(static p => !p.IsGeneratedLanding);
            catalog.TryGetValue(repo, out var meta);
            var bannerStem = DocsRepoCatalog.BannerStem(repo);
            var bannerPath = Path.Combine(output, "assets", "banners", bannerStem + ".svg");
            var banner = File.Exists(bannerPath)
                ? $"""<img class="repo-banner" src="assets/banners/{Html(bannerStem)}.svg" alt="{Html(repo)}"/>"""
                : $"""<div class="repo-banner text-banner">{Html(repo)}</div>""";

            var blurb = !string.IsNullOrWhiteSpace(meta?.Blurb)
                ? meta!.Blurb
                : "Library documentation from docs/. Docs opens the README landing page with full sidebar navigation.";
            var tag = !string.IsNullOrWhiteSpace(meta?.Tag) ? meta!.Tag : repo;
            var topics = TopicsHtml(meta?.Topics);
            var search = string.Join(' ', new[] { repo, tag, blurb }.Concat(meta?.Topics ?? Array.Empty<string>()))
                .ToLowerInvariant();

            cards.AppendLine($"""
                <article class="repo-card" data-search="{Html(search)}">
                  {banner}
                  <div class="repo-card-body">
                    <div class="repo-meta">
                      <span>{count} pages</span>
                      {(landing.IsGeneratedLanding ? "<span>generated overview</span>" : "<span>docs/README.md</span>")}
                    </div>
                    <h3>{Html(repo)}</h3>
                    <p class="repo-tagline">{Html(tag)}</p>
                    <p>{Html(blurb)}</p>
                    {topics}
                    <div class="card-actions">
                      <a class="btn-primary" href="{Html(landing.OutputRelativePath)}">Docs</a>
                      <a class="btn-secondary" href="https://github.com/{Html(options.Org)}/{Html(repo)}">Source</a>
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
              <meta name="description" content="Novolis documentation site — one docs tree per repository."/>
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
                  <a href="#libraries">Libraries</a>
                  <a href="https://github.com/{{Html(options.Org)}}">GitHub</a>
                </nav>
              </header>
              <main>
                <section class="hero hero-compact">
                  <div class="hero-content">
                    <img class="hero-logo" src="assets/brand/logo-brand-transparent.svg" alt="Novolis"/>
                    <p class="eyebrow">Documentation site</p>
                    <h1>Every library. One docs home.</h1>
                    <p class="hero-copy">Each card opens that repository's <code>docs/README.md</code> (or a generated overview) with sidebar navigation built from the docs folder layout.</p>
                  </div>
                  <div class="telemetry-panel" aria-label="Docs telemetry">
                    <div><span>{{byRepo.Count}}</span><strong>libraries</strong></div>
                    <div><span>{{byRepo.Values.Sum(static v => v.Count)}}</span><strong>pages</strong></div>
                    <div><span>docs/</span><strong>sparse corpus</strong></div>
                    <div><span>novolis-docs</span><strong>site builder</strong></div>
                  </div>
                </section>

                <section id="libraries" class="section">
                  <div class="section-heading">
                    <p class="eyebrow">Source and docs for every repository</p>
                    <h2>Libraries</h2>
                  </div>
                  <div class="controls">
                    <label class="search-box">
                      <span>Search</span>
                      <input type="search" id="portfolioSearch" placeholder="raylib, pdf, avalonia, docs"/>
                    </label>
                  </div>
                  <div class="repo-grid" id="repoGrid">
                    {{cards}}
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

    private static string DocShell(string title, string repo, string article, string sidebar, string org, string assetPrefix)
        => $"""
            <!doctype html>
            <html lang="en">
            <head>
              <meta charset="utf-8"/>
              <meta name="viewport" content="width=device-width, initial-scale=1"/>
              <meta name="description" content="Novolis documentation for {Html(repo)}"/>
              <title>{Html(title)} · {Html(repo)} · Novolis Docs</title>
              <link rel="icon" href="{assetPrefix}assets/brand/favicon.svg"/>
              <link rel="stylesheet" href="{assetPrefix}assets/site.css"/>
            </head>
            <body class="docs-site">
              <header class="topbar">
                <a class="brand" href="{assetPrefix}index.html" aria-label="Novolis docs home">
                  <img src="{assetPrefix}assets/brand/logo-icon.svg" alt=""/>
                  <span>Novolis Docs</span>
                </a>
                <nav>
                  <a href="{assetPrefix}index.html#libraries">Libraries</a>
                  <a href="https://github.com/{Html(org)}/{Html(repo)}">Source</a>
                  <a href="https://github.com/{Html(org)}">GitHub</a>
                </nav>
              </header>
              <div class="docs-layout">
                {sidebar}
                <main class="docs-main">
                  {article}
                </main>
              </div>
              <footer class="footer">
                <a href="{assetPrefix}index.html">All libraries</a>
                <a href="https://github.com/{Html(org)}/{Html(repo)}">GitHub source</a>
              </footer>
              <script src="{assetPrefix}assets/site.js"></script>
            </body>
            </html>
            """;

    private static void CopyAssets(DocsSiteOptions options, string output)
    {
        if (!string.IsNullOrWhiteSpace(options.AssetsDirectory) && Directory.Exists(options.AssetsDirectory))
        {
            foreach (var file in Directory.EnumerateFiles(options.AssetsDirectory))
            {
                File.Copy(file, Path.Combine(output, "assets", Path.GetFileName(file)), overwrite: true);
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

    private static string RewriteMarkdownLinks(
        string markdown,
        string currentDocsRelative,
        string currentOutputRelative,
        IReadOnlyDictionary<string, string> outputByDocsPath)
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

            var resolvedDocs = ResolveRelative(currentDocsRelative, pathPart);
            if (!outputByDocsPath.TryGetValue(resolvedDocs, out var targetOutput))
            {
                // Also try with docs/ prefix stripped already
                return match.Value;
            }

            var relativeHref = DocsNavBuilder.ToRepoLocalHref(targetOutput, currentOutputRelative);
            return $"[{label}]({relativeHref}{fragment})";
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

    private static string RenderBodyHtml(string markdown, DocsSitePage page)
    {
        try
        {
            return MarkdownDocument.Parse(markdown).ToHtml();
        }
        catch (Exception ex)
        {
            // Large org corpora include Markdown the Novolis subset parser does not yet accept.
            return $"""
                <div class="callout warn">
                  <p>Could not fully render <code>{Html(page.DocsRelativePath)}</code> ({Html(ex.GetType().Name)}). Showing source.</p>
                </div>
                <pre class="markdown-fallback"><code>{Html(markdown)}</code></pre>
                """;
        }
    }

    private static string StripLeadingH1(string markdown)
    {
        var match = Regex.Match(markdown, @"(?m)^\s*#\s+.+\r?\n+");
        return match.Success ? markdown.Remove(match.Index, match.Length) : markdown;
    }

    private static string? ResolveCatalogPath(DocsSiteOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CatalogPath))
        {
            return Path.GetFullPath(options.CatalogPath);
        }

        if (!string.IsNullOrWhiteSpace(options.AssetsDirectory))
        {
            var besideAssets = Path.Combine(Path.GetFullPath(options.AssetsDirectory), "..", "repo-catalog.json");
            if (File.Exists(besideAssets))
            {
                return Path.GetFullPath(besideAssets);
            }
        }

        return null;
    }

    private static string TopicsHtml(IReadOnlyList<string>? topics)
    {
        if (topics is null || topics.Count == 0)
        {
            return string.Empty;
        }

        var chips = string.Join(string.Empty, topics.Select(static t => $"<span>{Html(t)}</span>"));
        return $"""<div class="topic-row" aria-label="Topics">{chips}</div>""";
    }

    private static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static Encoding Utf8NoBom() => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}
