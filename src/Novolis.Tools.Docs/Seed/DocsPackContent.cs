using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Seed;

/// <summary>Markdown templates for the policy docs pack.</summary>
public static class DocsPackContent
{
    private const string Org = "Novolis-Platform";

    /// <summary>Portfolio docs site root URL.</summary>
    public static string DocsSiteRoot => $"https://{Org.ToLowerInvariant()}.github.io/.github";

    /// <summary>Per-repo docs site URL.</summary>
    public static string DocsSiteUrl(string repoName) =>
        string.Equals(repoName, ".github", StringComparison.OrdinalIgnoreCase)
            ? $"{DocsSiteRoot}/"
            : $"{DocsSiteRoot}/{repoName}/";

    /// <summary>docs/README.md body.</summary>
    public static string DocsReadme(string repoName, DocsRepoMeta meta, IReadOnlyList<string> existingDocs, IReadOnlyList<string> packages)
    {
        var site = DocsSiteUrl(repoName);
        var title = string.Equals(repoName, ".github", StringComparison.OrdinalIgnoreCase)
            ? "Novolis platform docs"
            : $"{repoName} documentation";

        var lines = new List<string>
        {
            $"# {title}",
            string.Empty,
            meta.Blurb,
            string.Empty,
            $"Published docs: [{site}]({site})",
            string.Empty,
            "## Guides",
            string.Empty,
            "| Doc | What it covers |",
            "| --- | --- |",
            "| [getting-started.md](getting-started.md) | Install, restore from GitHub Packages, first use |",
            "| [design.md](design.md) | Goals, layer placement, non-goals |",
            "| [release.md](release.md) | CalVer publish and package list |",
        };

        foreach (var doc in existingDocs
                     .Where(d => d is not ("README.md" or "getting-started.md" or "design.md" or "release.md") && !d.Contains('/'))
                     .OrderBy(static d => d, StringComparer.OrdinalIgnoreCase))
        {
            var label = Path.GetFileNameWithoutExtension(doc);
            lines.Add($"| [{doc}]({doc}) | {label} |");
        }

        lines.Add(string.Empty);
        if (packages.Count > 0)
        {
            lines.Add("## Packages");
            lines.Add(string.Empty);
            lines.Add("| Package |");
            lines.Add("| --- |");
            foreach (var pkg in packages)
            {
                lines.Add($"| `{pkg}` |");
            }

            lines.Add(string.Empty);
        }

        lines.Add("## More");
        lines.Add(string.Empty);
        lines.Add($"- [Org docs catalog]({DocsSiteRoot}/)");
        lines.Add("- [Repository README](../README.md)");
        lines.Add($"- [Governance](https://github.com/{Org}/novolis-governance)");
        lines.Add(string.Empty);
        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>docs/getting-started.md body.</summary>
    public static string GettingStarted(string repoName, DocsRepoMeta meta, IReadOnlyList<string> packages)
    {
        var site = DocsSiteUrl(repoName);
        var sample = packages.Count > 0 ? packages[0] : "Novolis.Example";
        return $"""
            # Getting started

            {meta.Blurb}

            Published guide: [{site}]({site})

            ## Prerequisites

            - [.NET 10 SDK](https://dotnet.microsoft.com/download)
            - GitHub Packages auth for ``Novolis.*`` (see [nuget-only-policy](https://github.com/{Org}/novolis-governance/blob/main/docs/nuget-only-policy.md))

            Configure GPR once from a sibling ``novolis-governance`` checkout:

            ```powershell
            pwsh -File d:\novolis\novolis-governance\scripts\configure-gpr-user-nuget.ps1
            ```

            ## Install

            ```bash
            dotnet add package {sample}
            ```

            Local multi-repo iteration uses ProjectReference mode via ``d:\novolis\Novolis.Platform.slnx`` — never a local NuGet folder feed.

            ## Next

            - [design.md](design.md) — layer placement and non-goals
            - [release.md](release.md) — publish cadence
            - [Org docs catalog]({DocsSiteRoot}/)
            """;
    }

    /// <summary>docs/design.md body.</summary>
    public static string Design(string repoName, DocsRepoMeta meta, IReadOnlyList<string> packages)
    {
        var site = DocsSiteUrl(repoName);
        var layer = DocsPackLayerHints.Get(repoName);
        var pkgList = packages.Count > 0
            ? string.Join(Environment.NewLine, packages.Select(p => $"- `{p}`"))
            : "- (no packable ``Novolis.*`` projects detected in ``src/`` / ``codegen/``)";
        var topics = string.Join(Environment.NewLine, meta.Topics.Select(t => $"- `{t}`"));

        return $"""
            # Design

            {meta.Blurb}

            Published docs: [{site}]({site})

            ## Layer placement

            {layer}

            ## Goals

            - Keep public APIs documented and packable as ``Novolis.*`` on GitHub Packages (when applicable).
            - Prefer BCL types and existing Novolis packages over parallel abstractions.
            - Document restore and ProjectReference-mode builds without local NuGet folder feeds.

            ## Non-goals

            - Local NuGet folder feeds or committed cross-repo ``ProjectReference`` into sibling checkouts.
            - Avalonia package references outside ``Novolis.Avalonia.*``.
            - Microsoft.Maui package references outside ``Novolis.Maui.*`` (except Voice.Platform.Maui).
            - Upward spine dependencies (e.g. Math → Simulation).

            ## Packages

            {pkgList}

            ## Topics

            {topics}
            """;
    }

    /// <summary>docs/release.md body.</summary>
    public static string Release(string repoName, IReadOnlyList<string> packages)
    {
        var site = DocsSiteUrl(repoName);
        var pkgList = packages.Count > 0
            ? string.Join(Environment.NewLine, packages.Select(p => $"- `{p}`"))
            : "- (no packable ``Novolis.*`` projects detected — see repository README)";

        return $"""
            # Release

            This repository publishes with the org CalVer scheme (``2026.1.*``) via ``merge.yml`` to GitHub Packages when packages are packable.

            See [release-policy](https://github.com/{Org}/novolis-governance/blob/main/docs/release-policy.md).

            Published docs: [{site}]({site})

            ## Packages

            {pkgList}

            ## Consumers

            Restore from nuget.org + ``https://nuget.pkg.github.com/{Org}/index.json`` only.

            Local multi-repo iteration: open ``d:\novolis\Novolis.Platform.slnx`` (ProjectReference mode) — do not add a local feed.
            """;
    }
}
