using System.Text.RegularExpressions;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Marketing;

/// <summary>README marketing block between <c>novolis-marketing</c> HTML comment markers.</summary>
public static partial class MarketingHeaderBuilder
{
    /// <summary>Start marker for the marketing header block.</summary>
    public const string StartMarker = "<!-- novolis-marketing:start -->";

    /// <summary>End marker for the marketing header block.</summary>
    public const string EndMarker = "<!-- novolis-marketing:end -->";

    private const string Org = "Novolis-Platform";

    private static readonly string BrandLogoUrl =
        $"https://raw.githubusercontent.com/{Org}/.github/main/brand/logo-brand-transparent.svg";

    [GeneratedRegex("(?s)<!-- novolis-marketing:start -->.*?<!-- novolis-marketing:end -->\\s*", RegexOptions.None)]
    private static partial Regex MarketingBlockRegex();

    [GeneratedRegex("(?s)<!-- novolis-package-index:start -->.*?<!-- novolis-package-index:end -->\\s*")]
    private static partial Regex PackageIndexBlockRegex();

    /// <summary>Builds the marketing header markdown block.</summary>
    /// <param name="repoName">Repository folder name (e.g. <c>novolis-tools</c>).</param>
    /// <param name="meta">Catalog tagline and blurb.</param>
    /// <param name="docsSiteUrl">When set, docs badge and links target the portfolio site; otherwise legacy GitHub pages paths.</param>
    public static string Build(string repoName, DocsRepoMeta meta, string? docsSiteUrl = null)
    {
        var bannerName = DocsRepoCatalog.BannerStem(repoName);
        var bannerUrl = $"https://raw.githubusercontent.com/{Org}/.github/main/brand/banners/{bannerName}.svg";
        var docsUrl = docsSiteUrl ?? (string.Equals(repoName, ".github", StringComparison.OrdinalIgnoreCase)
            ? $"https://{Org.ToLowerInvariant()}.github.io/.github/"
            : $"https://{Org.ToLowerInvariant()}.github.io/.github/{repoName}/");

        var mergeImg =
            $"https://img.shields.io/github/actions/workflow/status/{Org}/{repoName}/merge.yml?branch=main&label=merge&logo=github";
        var pkgImg = "https://img.shields.io/badge/packages-GitHub%20Packages-0a7ea3?logo=nuget";
        var docsImg = "https://img.shields.io/badge/docs-portfolio-0a7ea3";
        var orgImg = "https://img.shields.io/badge/org-Novolis--Platform-111827";

        return $"""
            {StartMarker}
            <p align="center">
              <a href="https://github.com/{Org}">
                <img src="{BrandLogoUrl}" width="360" alt="Novolis"/>
              </a>
            </p>

            <p align="center">
              <img src="{bannerUrl}" width="100%" alt="{repoName}"/>
            </p>

            <p align="center">
              <strong>{meta.Tag}</strong><br/>
              {meta.Blurb}
            </p>

            <p align="center">
              <a href="{docsUrl}"><img src="{docsImg}" alt="docs"/></a>
              <a href="https://github.com/{Org}/{repoName}/actions"><img src="{mergeImg}" alt="merge"/></a>
              <a href="https://github.com/orgs/{Org}/packages?repo_name={repoName}"><img src="{pkgImg}" alt="packages"/></a>
              <a href="https://github.com/{Org}"><img src="{orgImg}" alt="org"/></a>
            </p>

            <p align="center">
              <a href="{docsUrl}">Docs</a>
              ·
              <a href="https://nuget.pkg.github.com/{Org}/index.json"><code>https://nuget.pkg.github.com/{Org}/index.json</code></a>
              ·
              <a href="https://github.com/{Org}/.github/blob/main/profile/README.md">Org landing</a>
              ·
              <a href="https://github.com/{Org}/novolis-governance">Governance</a>
            </p>

            ---
            {EndMarker}


            """;
    }

    /// <summary>Replaces or prepends the marketing block in an existing README body.</summary>
    public static string MergeIntoReadme(string body, string header, bool prependWhenMissing = true)
    {
        if (MarketingBlockRegex().IsMatch(body))
        {
            return MarketingBlockRegex().Replace(body, header);
        }

        if (!prependWhenMissing)
        {
            return body;
        }

        if (PackageIndexBlockRegex().IsMatch(body))
        {
            return header + body;
        }

        return header + body.TrimStart();
    }

    /// <summary>Returns true when the text contains both marketing markers.</summary>
    public static bool HasMarketingBlock(string text) =>
        text.Contains(StartMarker, StringComparison.Ordinal) &&
        text.Contains(EndMarker, StringComparison.Ordinal);
}
