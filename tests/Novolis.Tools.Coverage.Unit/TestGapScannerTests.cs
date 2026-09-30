using Novolis.Tools.Coverage;

namespace Novolis.Tools.Coverage.Unit;

public sealed class TestGapScannerTests
{
    [Test]
    public async Task Scan_Flags_Packable_Without_Test_Reference()
    {
        var root = Path.Combine(Path.GetTempPath(), "test-gaps-" + Guid.NewGuid().ToString("N"));
        var src = Path.Combine(root, "novolis-demo", "src", "Novolis.Demo");
        var tests = Path.Combine(root, "novolis-demo", "tests", "Novolis.Demo.Unit");
        Directory.CreateDirectory(src);
        Directory.CreateDirectory(tests);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(src, "Novolis.Demo.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <IsPackable>true</IsPackable>
                    <PackageId>Novolis.Demo</PackageId>
                  </PropertyGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(tests, "Novolis.Demo.Unit.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <IsTestProject>true</IsTestProject>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="TUnit" />
                  </ItemGroup>
                </Project>
                """);
            Directory.CreateDirectory(Path.Combine(root, "novolis-governance", "scripts"));
            await File.WriteAllTextAsync(Path.Combine(root, "novolis-governance", "scripts", "coverage-excludes.txt"), "# none\n");

            var report = TestGapScanner.Scan(new TestGapOptions { Root = root, ThrottleLimit = 1 });
            await Assert.That(report.UntestedAssemblies.Count).IsEqualTo(1);
            await Assert.That(report.UntestedAssemblies[0].PackageId).IsEqualTo("Novolis.Demo");
            await Assert.That(report.GapCount).IsGreaterThan(0);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
