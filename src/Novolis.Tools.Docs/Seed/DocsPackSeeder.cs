using Novolis.Tools.Docs.Marketing;
using Novolis.Tools.Docs.Org;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Seed;

/// <summary>Seeds docs/README + policy guides when missing or thin.</summary>
public static class DocsPackSeeder
{
    private const string Org = "Novolis-Platform";

    private static readonly HashSet<string> SkipLocalOnly = new(StringComparer.OrdinalIgnoreCase)
    {
        "novolis-experimental", "novolis-mapping", "novolis-scheduling", "novolis-wirefish",
    };

    /// <summary>Runs docs pack seeding across the workspace.</summary>
    public static DocsPackSeedResult Run(DocsPackSeedOptions options)
    {
        var catalogPath = Path.Combine(options.GitHubBrandRoot, "site", "repo-catalog.json");
        IReadOnlyDictionary<string, DocsRepoMeta> catalog = File.Exists(catalogPath)
            ? PlatformRepoCatalog.LoadFromFile(catalogPath)
            : PlatformRepoCatalog.Entries;

        if (catalog.Count == 0 && File.Exists(catalogPath))
        {
            throw new InvalidOperationException($"Missing catalog: {catalogPath} — run novolis-docs marketing first.");
        }

        var result = new DocsPackSeedResult();
        var repoDirs = Directory.GetDirectories(options.WorkspaceRoot)
            .Select(d => new DirectoryInfo(d))
            .Where(d => (d.Name.StartsWith("novolis-", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(d.Name, ".github", StringComparison.OrdinalIgnoreCase)) &&
                        !SkipLocalOnly.Contains(d.Name))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (options.OnlyRepos.Count > 0)
        {
            var only = new HashSet<string>(options.OnlyRepos, StringComparer.OrdinalIgnoreCase);
            repoDirs = repoDirs.Where(d => only.Contains(d.Name)).ToArray();
        }

        foreach (var dir in repoDirs)
        {
            var name = dir.Name;
            if (!catalog.ContainsKey(name) && !string.Equals(name, ".github", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var archived = GhProcess.RunGh(["repo", "view", $"{Org}/{name}", "--json", "isArchived", "-q", ".isArchived"]).Trim();
                    if (string.Equals(archived, "true", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }
                catch
                {
                    continue;
                }
            }

            var meta = catalog.TryGetValue(name, out var m) ? m : PlatformRepoCatalog.GetOrDefault(name);
            var docsDir = Path.Combine(dir.FullName, "docs");
            Directory.CreateDirectory(docsDir);

            var packages = PackablePackageScanner.Scan(dir.FullName);
            var existing = Directory.Exists(docsDir)
                ? Directory.GetFiles(docsDir, "*.md").Select(Path.GetFileName).Where(n => n is not null).Cast<string>().ToArray()
                : [];

            Console.WriteLine($"==> {name}");

            var readmePath = Path.Combine(docsDir, "README.md");
            if (DocsPackThinDoc.ShouldReplace(readmePath, DocsPackDocKind.Readme, name, options.OverwriteThin))
            {
                WriteUtf8NoBom(readmePath, DocsPackContent.DocsReadme(name, meta, existing, packages));
                result.Readme++;
                Console.WriteLine("  + docs/README.md");
            }

            var gsPath = Path.Combine(docsDir, "getting-started.md");
            if (DocsPackThinDoc.ShouldReplace(gsPath, DocsPackDocKind.GettingStarted, name, options.OverwriteThin))
            {
                WriteUtf8NoBom(gsPath, DocsPackContent.GettingStarted(name, meta, packages));
                result.GettingStarted++;
                Console.WriteLine("  + docs/getting-started.md");
            }

            var designPath = Path.Combine(docsDir, "design.md");
            if (DocsPackThinDoc.ShouldReplace(designPath, DocsPackDocKind.Design, name, options.OverwriteThin))
            {
                WriteUtf8NoBom(designPath, DocsPackContent.Design(name, meta, packages));
                result.Design++;
                Console.WriteLine("  + docs/design.md");
            }

            var releasePath = Path.Combine(docsDir, "release.md");
            if (DocsPackThinDoc.ShouldReplace(releasePath, DocsPackDocKind.Release, name, options.OverwriteThin))
            {
                WriteUtf8NoBom(releasePath, DocsPackContent.Release(name, packages));
                result.Release++;
                Console.WriteLine("  + docs/release.md");
            }

            if (!options.SkipMarketing && DocsPackMarketingUpdater.Update(dir.FullName, name, meta))
            {
                result.Marketing++;
                Console.WriteLine("  ~ README marketing / Docs link");
            }

            result.Repos++;
        }

        Console.WriteLine();
        Console.WriteLine(
            $"Done. Repos={result.Repos} docsREADME={result.Readme} gettingStarted={result.GettingStarted} design={result.Design} release={result.Release} marketing={result.Marketing}");
        return result;
    }

    private static void WriteUtf8NoBom(string path, string content) =>
        File.WriteAllText(path, content.TrimEnd() + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
}
