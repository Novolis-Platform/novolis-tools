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
