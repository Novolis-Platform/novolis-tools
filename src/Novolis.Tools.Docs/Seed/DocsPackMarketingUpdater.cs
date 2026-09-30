using System.Text.RegularExpressions;
using Novolis.Tools.Docs.Marketing;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Seed;

/// <summary>Updates root README marketing with portfolio docs links.</summary>
public static partial class DocsPackMarketingUpdater
{
    [GeneratedRegex("(?s)(<!-- novolis-marketing:end -->\\s*)")]
    private static partial Regex AfterMarketingEndRegex();

    /// <summary>Rewrites or seeds the repository README marketing block; returns true when written.</summary>
    public static bool Update(string repoRoot, string repoName, DocsRepoMeta meta)
    {
        var readmePath = Path.Combine(repoRoot, "README.md");
        var site = DocsPackContent.DocsSiteUrl(repoName);
        var header = MarketingHeaderBuilder.Build(repoName, meta, site);

        if (!File.Exists(readmePath))
        {
            var seed = $"""
                {header}# {repoName}

                {meta.Blurb}

                ## Documentation

                - [Docs site]({site})
                - [Getting started](docs/getting-started.md)
                """;
            WriteUtf8NoBom(readmePath, seed);
            return true;
        }

        var body = File.ReadAllText(readmePath);
        var changed = false;
        if (MarketingHeaderBuilder.HasMarketingBlock(body))
        {
            body = MarketingHeaderBuilder.MergeIntoReadme(body, header);
            changed = true;
        }
        else
        {
            body = header + body.TrimStart();
            changed = true;
        }

        if (!body.Contains(site, StringComparison.Ordinal))
        {
            var docsBlock = $"""

                ## Documentation

                - [Docs site]({site}) — rendered guides from ``docs/``
                - [getting-started.md](docs/getting-started.md)
                - [design.md](docs/design.md)
                - [release.md](docs/release.md)

                """;
            if (AfterMarketingEndRegex().IsMatch(body))
            {
                body = AfterMarketingEndRegex().Replace(body, $"$1{docsBlock}");
            }
            else
            {
                body = body.TrimEnd() + docsBlock;
            }

            changed = true;
        }

        if (changed)
        {
            WriteUtf8NoBom(readmePath, body.TrimEnd() + Environment.NewLine);
        }

        return changed;
    }

    private static void WriteUtf8NoBom(string path, string content) =>
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}
