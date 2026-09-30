using Novolis.Tools.Coverage;

namespace Novolis.Tools.Coverage.Unit;

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
