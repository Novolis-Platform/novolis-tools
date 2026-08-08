using Novolis.Tools.Coverage;

namespace Novolis.Tools.Coverage.Unit;

public sealed class CoverageWorkspaceTests
{
    [Test]
    public async Task ExpandNames_Splits_Commas_And_Trims()
    {
        var names = CoverageWorkspace.ExpandNames(["a, b", "c"]);
        await Assert.That(names).IsEquivalentTo(["a", "b", "c"]);
    }

    [Test]
    public async Task RepoAssemblyFilter_Uses_Home_Prefixes()
    {
        await Assert.That(CoverageWorkspace.RepoAssemblyFilter("novolis-gaming")).IsEqualTo("+Novolis.Game*");
        await Assert.That(CoverageWorkspace.RepoAssemblyFilter("novolis-xsd")).IsEqualTo("+Novolis.Xsd*");
        await Assert.That(CoverageWorkspace.RepoAssemblyFilter("novolis-analyzers"))
            .Contains("Novolis.Analyzers");
        await Assert.That(CoverageWorkspace.RepoAssemblyFilter("novolis-analyzers"))
            .Contains("-Novolis.Analyzers.Licensing");
    }

    [Test]
    public async Task ReadExcludes_Merges_File_And_Extra()
    {
        var path = Path.Combine(Path.GetTempPath(), "cov-ex-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            await File.WriteAllTextAsync(path, """
                # comment
                novolis-apps
                novolis-raylib
                """);
            var set = CoverageWorkspace.ReadExcludes(path, ["novolis-audio"]);
            await Assert.That(set.Contains("novolis-apps")).IsTrue();
            await Assert.That(set.Contains("novolis-raylib")).IsTrue();
            await Assert.That(set.Contains("novolis-audio")).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public sealed class CoberturaSummaryParserTests
{
    [Test]
    public async Task Parse_Reads_Rates()
    {
        var path = Path.Combine(Path.GetTempPath(), "cov-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            await File.WriteAllTextAsync(path, """
                <?xml version="1.0" encoding="utf-8"?>
                <coverage line-rate="0.936" branch-rate="0.724" lines-covered="100" lines-valid="107" branches-covered="50" branches-valid="69" version="1.0" timestamp="0">
                </coverage>
                """);
            var sum = CoberturaSummaryParser.Parse(path);
            await Assert.That(sum.LinePercent).IsEqualTo(93.6);
            await Assert.That(sum.BranchPercent).IsEqualTo(72.4);
            await Assert.That(sum.LinesCovered).IsEqualTo(100);
            await Assert.That(sum.LinesValid).IsEqualTo(107);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

public sealed class TestHostDiscoveryTests
{
    [Test]
    public async Task IsTestHostProject_Requires_Tests_Path_And_TUnit()
    {
        var dir = Path.Combine(Path.GetTempPath(), "cov-host-" + Guid.NewGuid().ToString("N"), "tests", "Demo.Unit");
        Directory.CreateDirectory(dir);
        var proj = Path.Combine(dir, "Demo.Unit.csproj");
        try
        {
            await File.WriteAllTextAsync(proj, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <IsTestProject>true</IsTestProject>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="TUnit" />
                  </ItemGroup>
                </Project>
                """);
            await Assert.That(TestHostDiscovery.IsTestHostProject(proj)).IsTrue();

            var support = Path.Combine(dir, "Demo.TestSupport.csproj");
            await File.WriteAllTextAsync(support, """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <IsTestProject>false</IsTestProject>
                  </PropertyGroup>
                </Project>
                """);
            await Assert.That(TestHostDiscovery.IsTestHostProject(support)).IsFalse();
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(dir))!, recursive: true);
        }
    }

    [Test]
    public async Task DiscoverFromPlatformSlnx_Groups_By_Repo()
    {
        var root = Path.Combine(Path.GetTempPath(), "cov-slnx-" + Guid.NewGuid().ToString("N"));
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
        await File.WriteAllTextAsync(slnx, $"""
            <Solution>
              <Folder Name="/novolis-demo/tests/">
                <Project Path="novolis-demo\tests\Novolis.Demo.Unit\Novolis.Demo.Unit.csproj" />
              </Folder>
            </Solution>
            """);

        try
        {
            var repos = TestHostDiscovery.DiscoverFromPlatformSlnx(
                root,
                slnx,
                exclude: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                include: null);
            await Assert.That(repos.Count).IsEqualTo(1);
            await Assert.That(repos[0].Name).IsEqualTo("novolis-demo");
            await Assert.That(repos[0].TestProjects.Count).IsEqualTo(1);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

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

public sealed class CoverageAnalyzerTests
{
    [Test]
    public async Task Shortfall_And_Gaps_From_Document()
    {
        var path = Path.Combine(Path.GetTempPath(), "cov-doc-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            await File.WriteAllTextAsync(path, """
                <?xml version="1.0" encoding="utf-8"?>
                <coverage line-rate="0.90" branch-rate="0.80" lines-covered="90" lines-valid="100" branches-covered="80" branches-valid="100" version="1.0" timestamp="0">
                  <packages>
                    <package name="Demo.A" line-rate="1" branch-rate="0.5" complexity="1">
                      <classes>
                        <class name="Demo.A.C" filename="C.cs" line-rate="1" branch-rate="0.5" complexity="1">
                          <methods />
                          <lines>
                            <line number="1" hits="1" branch="false" />
                            <line number="2" hits="1" branch="true" condition-coverage="50% (1/2)" />
                            <line number="3" hits="0" branch="false" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                    <package name="Demo.B" line-rate="1" branch-rate="1" complexity="1">
                      <classes>
                        <class name="Demo.B.C" filename="C.cs" line-rate="1" branch-rate="1" complexity="1">
                          <methods />
                          <lines>
                            <line number="1" hits="1" branch="false" />
                          </lines>
                        </class>
                      </classes>
                    </package>
                  </packages>
                </coverage>
                """);

            var doc = CoberturaDocumentParser.Load(path);
            await Assert.That(doc.Packages.Count).IsEqualTo(2);
            var a = doc.Packages.Single(p => p.Name == "Demo.A");
            await Assert.That(a.LinesValid).IsEqualTo(3);
            await Assert.That(a.LinesCovered).IsEqualTo(2);
            await Assert.That(a.BranchGap).IsEqualTo(1);

            var shortfall = CoverageAnalyzer.Shortfall(doc.Summary, 95);
            await Assert.That(shortfall.LinesNeeded).IsEqualTo(5);
            await Assert.That(shortfall.BranchesNeeded).IsEqualTo(15);
            await Assert.That(shortfall.MeetsTarget).IsFalse();

            var below = CoverageAnalyzer.PackagesBelowTarget(doc, 95, take: 10);
            await Assert.That(below.Any(p => p.Name == "Demo.A")).IsTrue();

            var md = CoverageAnalyzer.FormatGapsMarkdown(doc, 95, take: 5);
            await Assert.That(md).Contains("Demo.A");
            await Assert.That(md).Contains("Coverage gaps");

            var (failed, message) = CoverageGate.Evaluate(doc.Summary, 95);
            await Assert.That(failed).IsTrue();
            await Assert.That(message).Contains("line coverage");

            await Assert.That(() => CoverageAssert.AtLeast(doc.Summary, 95))
                .Throws<InvalidOperationException>();

            CoverageAssert.AtLeast(doc.Summary, failBelow: -1);
            CoverageAssert.AtLeast(path, failBelow: -1);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Gate_Passes_When_Above_Target()
    {
        var summary = new CoberturaSummary
        {
            LinePercent = 96,
            BranchPercent = 96,
            LinesCovered = 96,
            LinesValid = 100,
            BranchesCovered = 96,
            BranchesValid = 100,
        };
        var (failed, message) = CoverageGate.Evaluate(summary, 95);
        await Assert.That(failed).IsFalse();
        await Assert.That(message).IsNull();
        CoverageAssert.AtLeast(summary, 95);
    }
}
