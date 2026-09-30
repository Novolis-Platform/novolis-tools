using System.Security;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Marketing;

/// <summary>Generates per-repo SVG banners under <c>brand/banners/</c>.</summary>
public static class MarketingBannerSvg
{
    /// <summary>Writes a banner SVG for the repository.</summary>
    public static void Write(string repoName, DocsRepoMeta meta, GraphicalProfileTokens profile, string outputPath)
    {
        var title = repoName.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase)
            ? repoName["novolis-".Length..]
            : string.Equals(repoName, ".github", StringComparison.OrdinalIgnoreCase) ? "platform" : repoName;

        var escTag = SecurityElement.Escape(meta.Tag) ?? meta.Tag;
        var escTitle = SecurityElement.Escape(title) ?? title;

        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1200 320" width="1200" height="320" role="img" aria-label="Novolis {escTitle}">
              <rect width="1200" height="320" fill="{profile.BackgroundDark}"/>
              <rect x="0" y="0" width="1200" height="4" fill="{profile.AccentDark}"/>
              <text x="56" y="118" fill="{profile.AccentDark}" font-family="{profile.FontFamily}" font-size="{profile.EyebrowSize}" font-weight="{profile.EyebrowWeight}" letter-spacing="{profile.WordmarkTracking}">NOVOLIS</text>
              <text x="56" y="188" fill="{profile.TextDark}" font-family="{profile.FontFamily}" font-size="64" font-weight="700">{escTitle}</text>
              <text x="56" y="248" fill="{profile.MutedDark}" font-family="{profile.FontFamily}" font-size="26">{escTag}</text>
              <rect x="56" y="280" width="180" height="4" rx="2" fill="{profile.AccentFillDark}"/>
            </svg>
            """;

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(outputPath, svg, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
