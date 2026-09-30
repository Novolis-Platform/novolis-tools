using Novolis.Tools.Docs;
using Novolis.Tools.Docs.Graph;
using Novolis.Tools.Docs.Markdown;
using Novolis.Tools.Docs.Marketing;
using Novolis.Tools.Docs.Mermaid;
using Novolis.Tools.Docs.Seed;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Unit;

public sealed class ProjectGraphScannerTests
{
    [Test]
    public async Task Scan_Finds_ProjectReference_Edge()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-scan-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "A"));
        Directory.CreateDirectory(Path.Combine(root, "B"));
        await File.WriteAllTextAsync(
            Path.Combine(root, "A", "A.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <ProjectReference Include="..\B\B.csproj" />
                <PackageReference Include="Novolis.Math.Core" Version="2026.1.*" />
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);
        await File.WriteAllTextAsync(
            Path.Combine(root, "B", "B.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk" />
            """);

        try
        {
            var graph = ProjectGraphScanner.Scan(root, includePackages: true, includeNovolisPackagesOnly: true);
            await Assert.That(graph.Edges.Any(e =>
                e.FromId == "A" && e.ToId == "B" && e.Label == "ProjectReference")).IsTrue();
            await Assert.That(graph.Edges.Any(e => e.ToId == "Novolis.Math.Core")).IsTrue();
            await Assert.That(graph.Edges.Any(e => e.ToId == "Newtonsoft.Json")).IsFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
