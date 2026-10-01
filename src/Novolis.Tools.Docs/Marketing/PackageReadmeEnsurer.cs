using System.Text.RegularExpressions;

namespace Novolis.Tools.Docs.Marketing;

/// <summary>Ensures packable project README.md files have brand strip and Install section.</summary>
public static partial class PackageReadmeEnsurer
{
    private const string Org = "Novolis-Platform";

    [GeneratedRegex("(?s)<!-- novolis-pkg-brand:start -->.*?<!-- novolis-pkg-brand:end -->\\s*")]
    private static partial Regex BrandBlockRegex();

    [GeneratedRegex("(?m)^## Install\\s*$")]
    private static partial Regex InstallHeadingRegex();

    [GeneratedRegex("(?m)^## Installation\\s*$")]
    private static partial Regex InstallationHeadingRegex();

    /// <summary>Creates or updates a package README; returns true when the file was written.</summary>
    public static bool Ensure(string csprojPath, string repoName)
    {
        if (!CsprojPackaging.GetEffectiveIsPackable(csprojPath))
        {
            return false;
        }

        var dir = Path.GetDirectoryName(csprojPath)!;
        var id = CsprojPackaging.GetPackageId(csprojPath);
        var desc = CsprojPackaging.GetDescription(csprojPath) ?? $"{id} — Novolis platform library.";
        var readmePath = Path.Combine(dir, "README.md");
        var docsUrl = $"https://{Org.ToLowerInvariant()}.github.io/.github/{repoName}/";
        var brandIconUrl = $"https://raw.githubusercontent.com/{Org}/.github/main/brand/logo-icon.png";
        var brandStrip = $"""
            [![Novolis]({brandIconUrl})]({docsUrl})

            [Novolis](https://github.com/{Org}) · [Docs]({docsUrl}) · [Source](https://github.com/{Org}/{repoName})

            """;

        if (!File.Exists(readmePath))
        {
            var content = $"""
                <!-- novolis-pkg-brand:start -->
                {brandStrip}<!-- novolis-pkg-brand:end -->

                # {id}

                {desc}

                ## Install

                ```bash
                dotnet add package {id}
                ```

                **Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (net10.0).

                ## Quick start

                ```csharp
                // See the repository docs and related package READMEs for entry points.
                using {id};
                ```

                ## More documentation

                - [Repository](https://github.com/{Org}/{repoName})
                - [Org landing](https://github.com/{Org})

                ## Support

                Pre-release packages publish continuously to GitHub Packages as 2026.1.*.
                """;
            WriteUtf8NoBom(readmePath, content.TrimEnd() + Environment.NewLine);
            return true;
        }

        var body = File.ReadAllText(readmePath);
        var changed = false;
        var markedStrip = $"<!-- novolis-pkg-brand:start -->{Environment.NewLine}{brandStrip}<!-- novolis-pkg-brand:end -->{Environment.NewLine}{Environment.NewLine}";
        if (body.Contains("novolis-pkg-brand:start", StringComparison.Ordinal))
        {
            var replaced = BrandBlockRegex().Replace(body, markedStrip);
            if (!string.Equals(replaced, body, StringComparison.Ordinal))
            {
                body = replaced;
                changed = true;
            }
        }
        else if (!body.Contains("logo-icon.png", StringComparison.Ordinal))
        {
            body = markedStrip + body.TrimStart();
            changed = true;
        }

        if (!InstallHeadingRegex().IsMatch(body) && !InstallationHeadingRegex().IsMatch(body))
        {
            var install = $"""

                ## Install

                ```bash
                dotnet add package {id}
                ```

                **Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (net10.0).
                """;
            var h1Match = Regex.Match(body, "(?s)^(#[^\n]+\n(?:(?!^#)[^\n]*\n)*)");
            if (h1Match.Success)
            {
                body = body.Insert(h1Match.Length, install);
            }
            else
            {
                body = body.TrimEnd() + install + Environment.NewLine;
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
