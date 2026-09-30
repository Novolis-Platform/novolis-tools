using Novolis.Tools.Coverage;

namespace Novolis.Tools.Coverage.Unit;

public sealed class CoverageCollectorListOnlyTests
{
    [Test]
    public async Task ResolveRoot_And_PlatformSlnx_And_ListOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "cov-list-" + Guid.NewGuid().ToString("N"));
        var repoTests = Path.Combine(root, "novolis-demo", "tests", "Novolis.Demo.Unit");
        Directory.CreateDirectory(repoTests);
        var proj = Path.Combine(repoTests, "Novolis.Demo.Unit.csproj");
        await File.WriteAllTextAsync(proj, """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><IsTestProject>true</IsTestProject></PropertyGroup>
              <ItemGroup><PackageReference Include="TUnit" /></ItemGroup>
            </Project>
            """);
        var slnx = Path.Combine(root, "Novolis.Platform.slnx");
        await File.WriteAllTextAsync(slnx, """
            <Solution>
              <Folder Name="/novolis-demo/tests/">
                <Project Path="novolis-demo\tests\Novolis.Demo.Unit\Novolis.Demo.Unit.csproj" />
              </Folder>
            </Solution>
            """);
        var outDir = Path.Combine(root, "out");
        Directory.CreateDirectory(outDir);

        try
        {
            await Assert.That(CoverageWorkspace.ResolveRoot(root)).IsEqualTo(Path.GetFullPath(root));
            await Assert.That(CoverageWorkspace.ResolvePlatformSlnx(root)).IsEqualTo(Path.GetFullPath(slnx));
            await Assert.That(CoverageWorkspace.DefaultExcludeFile(root))
                .Contains("coverage-excludes.txt");
            await Assert.That(CoverageWorkspace.RepoAssemblyFilter("novolis-foo-bar"))
                .IsEqualTo("+Novolis.Foo.Bar*");
            await Assert.That(CoverageWorkspace.ExpandNames(null).Count).IsEqualTo(0);

            await using var log = new StringWriter();
            var collector = new CoverageCollector(log);
            var result = await collector.CollectAsync(new CoverageCollectOptions
            {
                Root = root,
                OutputDir = outDir,
                PlatformSlnx = true,
                ListOnly = true,
                FailBelow = -1,
            });
            await Assert.That(result.Repos.Count).IsEqualTo(0);
            await Assert.That(log.ToString()).Contains("novolis-demo");

            var discovered = TestHostDiscovery.DiscoverRepos(
                root,
                exclude: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                include: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "novolis-demo" });
            await Assert.That(discovered.Count).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task CoberturaParser_MissingAttrs_DefaultsToZero()
    {
        var path = Path.Combine(Path.GetTempPath(), "cov-empty-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            await File.WriteAllTextAsync(path, """
                <?xml version="1.0" encoding="utf-8"?>
                <coverage version="1.0" timestamp="0">
                </coverage>
                """);
            var sum = CoberturaSummaryParser.Parse(path);
            await Assert.That(sum.LinePercent).IsEqualTo(0);
            await Assert.That(sum.BranchPercent).IsEqualTo(0);
            await Assert.That(sum.LinesCovered).IsEqualTo(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task CoverageWorkspace_Resolve_And_Models_Cover_Edges()
    {
        var root = Path.Combine(Path.GetTempPath(), "cov-ws-" + Guid.NewGuid().ToString("N"));
        var gov = Path.Combine(root, "novolis-governance");
        Directory.CreateDirectory(gov);
        var buildSlnx = Path.Combine(gov, "build");
        Directory.CreateDirectory(buildSlnx);
        var slnx = Path.Combine(buildSlnx, "Novolis.Platform.slnx");
        await File.WriteAllTextAsync(slnx, "<Solution />");

        try
        {
            await Assert.That(CoverageWorkspace.ResolveRoot(root)).IsEqualTo(Path.GetFullPath(root));
            await Assert.That(CoverageWorkspace.ResolvePlatformSlnx(root)).IsEqualTo(Path.GetFullPath(slnx));
            await Assert.That(() => CoverageWorkspace.ResolvePlatformSlnx(root, Path.Combine(root, "missing.slnx")))
                .Throws<FileNotFoundException>();
            await Assert.That(() => CoverageWorkspace.ResolvePlatformSlnx(Path.Combine(root, "empty")))
                .Throws<FileNotFoundException>();

            var excludes = CoverageWorkspace.ReadExcludes(Path.Combine(root, "nope.txt"), null);
            await Assert.That(excludes.Count).IsEqualTo(0);
            await Assert.That(CoverageWorkspace.ExpandNames(["", "  ", "a,,b"]).Count).IsEqualTo(2);
            await Assert.That(CoverageWorkspace.RepoAssemblyFilter("other-repo")).IsEqualTo("-Novolis.Analyzers.Licensing");

            var repoResult = new CoverageRepoResult
            {
                Repo = "novolis-demo",
                Status = "ok",
                Error = null,
                CoberturaFiles = [],
                LinePercent = 99,
                BranchPercent = 98,
                LinesCovered = 10,
                LinesValid = 10,
            };
            await Assert.That(repoResult.Repo).IsEqualTo("novolis-demo");
            await Assert.That(repoResult.Status).IsEqualTo("ok");
            await Assert.That(repoResult.LinePercent).IsEqualTo(99);

            var collect = new CoverageCollectResult
            {
                OutputDir = root,
                HtmlIndexPath = null,
                SummaryMarkdownPath = Path.Combine(root, "SUMMARY.md"),
                DurationSeconds = 1,
                Repos = [repoResult],
                GateFailed = false,
            };
            await Assert.That(collect.Repos.Count).IsEqualTo(1);
            await Assert.That(collect.GateFailed).IsFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public async Task CoverageWorkspace_ResolveRoot_Env_Walk_And_Throw()
    {
        var root = Path.Combine(Path.GetTempPath(), "cov-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "novolis-governance"));
        var previous = Environment.GetEnvironmentVariable("NOVOLIS_ROOT");
        var previousCwd = Directory.GetCurrentDirectory();
        try
        {
            Environment.SetEnvironmentVariable("NOVOLIS_ROOT", root);
            await Assert.That(CoverageWorkspace.ResolveRoot()).IsEqualTo(Path.GetFullPath(root));

            Environment.SetEnvironmentVariable("NOVOLIS_ROOT", "   ");
            var nested = Path.Combine(root, "child");
            Directory.CreateDirectory(nested);
            Directory.SetCurrentDirectory(nested);
            await Assert.That(CoverageWorkspace.ResolveRoot()).IsEqualTo(Path.GetFullPath(root));

            Environment.SetEnvironmentVariable("NOVOLIS_ROOT", Path.Combine(root, "missing-env-dir"));
            await Assert.That(CoverageWorkspace.ResolveRoot()).IsEqualTo(Path.GetFullPath(root));

            var rootSlnx = Path.Combine(root, "Novolis.Platform.slnx");
            await File.WriteAllTextAsync(rootSlnx, "<Solution />");
            await Assert.That(CoverageWorkspace.ResolvePlatformSlnx(root, rootSlnx))
                .IsEqualTo(Path.GetFullPath(rootSlnx));

            var orphan = Path.Combine(Path.GetTempPath(), "cov-orphan-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(orphan);
            Directory.SetCurrentDirectory(orphan);
            await Assert.That(() => CoverageWorkspace.ResolveRoot()).Throws<InvalidOperationException>();
        }
        finally
        {
            Directory.SetCurrentDirectory(previousCwd);
            Environment.SetEnvironmentVariable("NOVOLIS_ROOT", previous);
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
