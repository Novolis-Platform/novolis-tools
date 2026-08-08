using Novolis.Tools.Docs;
using Novolis.Tools.Docs.Graph;
using Novolis.Tools.Docs.Markdown;
using Novolis.Tools.Docs.Mermaid;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Unit;

public sealed class MarkdownDocumentTests
{
    [Test]
    public async Task ToMarkdown_Includes_Mermaid_Fence()
    {
        var md = new MarkdownDocument("Demo")
            .H1("Demo")
            .Mermaid(MermaidDiagram.Flowchart(c => c.Edge("a", "b")));

        var text = md.ToMarkdown();
        await Assert.That(text).Contains("```mermaid");
        await Assert.That(text).Contains("flowchart");
        await Assert.That(text).Contains("a --> b");
    }
}

public sealed class RelationshipGraphTests
{
    [Test]
    public async Task ToMermaidFlowchart_Uses_Stadium_For_Packages()
    {
        var graph = new RelationshipGraph()
            .AddNode("App", "App", GraphNodeKind.Project)
            .AddNode("Novolis.Math", "Novolis.Math", GraphNodeKind.Package)
            .AddEdge("App", "Novolis.Math", "PackageReference");

        var mermaid = graph.ToMermaidFlowchart();
        await Assert.That(mermaid).Contains("([\"Novolis.Math\"])");
        await Assert.That(mermaid).Contains("-.->");
    }
}

public sealed class DocPackBuilderTests
{
    [Test]
    public async Task Scaffold_Writes_Expected_Files()
    {
        var dir = Path.Combine(Path.GetTempPath(), "novolis-docs-" + Guid.NewGuid().ToString("N"));
        try
        {
            var written = DocPackBuilder.Scaffold("Sample").WriteTo(dir);
            await Assert.That(written.Count).IsEqualTo(4);
            await Assert.That(File.Exists(Path.Combine(dir, "overview.md"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(dir, "architecture.md"))).IsTrue();
            await Assert.That(await File.ReadAllTextAsync(Path.Combine(dir, "architecture.md")))
                .Contains("```mermaid");
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}

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

public sealed class DocsSiteBuilderTests
{
    [Test]
    public async Task Build_Creates_Repo_Site_With_Sidebar_And_Docs_Landing()
    {
        var corpus = Path.Combine(Path.GetTempPath(), "novolis-docs-corpus-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(Path.GetTempPath(), "novolis-docs-site-" + Guid.NewGuid().ToString("N"));
        try
        {
            var docsDir = Path.Combine(corpus, "novolis-sample", "docs");
            Directory.CreateDirectory(docsDir);
            await File.WriteAllTextAsync(
                Path.Combine(docsDir, "README.md"),
                """
                # Sample docs

                Welcome to the sample library docs.
                """);
            await File.WriteAllTextAsync(
                Path.Combine(docsDir, "getting-started.md"),
                """
                # Getting started

                Install the package and call `Hello()`.

                See [design](design.md).
                """);
            await File.WriteAllTextAsync(
                Path.Combine(docsDir, "design.md"),
                """
                # Design

                Layer edges point downward.
                """);

            var count = DocsSiteBuilder.Build(new DocsSiteOptions
            {
                CorpusDirectory = corpus,
                OutputDirectory = output,
                Org = "Novolis-Platform",
            });

            await Assert.That(count).IsEqualTo(3);
            await Assert.That(File.Exists(Path.Combine(output, "index.html"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(output, "novolis-sample", "index.html"))).IsTrue();
            await Assert.That(File.Exists(Path.Combine(output, "novolis-sample", "getting-started.html"))).IsTrue();

            var catalog = await File.ReadAllTextAsync(Path.Combine(output, "index.html"));
            await Assert.That(catalog).Contains(">Docs<");
            await Assert.That(catalog).Contains(">Source<");
            await Assert.That(catalog).Contains("novolis-sample/index.html");

            var landing = await File.ReadAllTextAsync(Path.Combine(output, "novolis-sample", "index.html"));
            await Assert.That(landing).Contains("docs-nav");
            await Assert.That(landing).Contains("Required");
            await Assert.That(landing).Contains("getting-started.html");
            await Assert.That(landing).Contains("Welcome to the sample library docs");

            var gettingStarted = await File.ReadAllTextAsync(Path.Combine(output, "novolis-sample", "getting-started.html"));
            await Assert.That(gettingStarted).Contains("Install the package");
            await Assert.That(gettingStarted).Contains("design.html");
            await Assert.That(gettingStarted).Contains("class=\"active\"");
        }
        finally
        {
            if (Directory.Exists(corpus))
            {
                Directory.Delete(corpus, recursive: true);
            }

            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Test]
    public async Task Build_Generates_Overview_When_Readme_Missing()
    {
        var corpus = Path.Combine(Path.GetTempPath(), "novolis-docs-corpus-" + Guid.NewGuid().ToString("N"));
        var output = Path.Combine(Path.GetTempPath(), "novolis-docs-site-" + Guid.NewGuid().ToString("N"));
        try
        {
            var docsDir = Path.Combine(corpus, "novolis-bare", "docs");
            Directory.CreateDirectory(docsDir);
            await File.WriteAllTextAsync(Path.Combine(docsDir, "getting-started.md"), "# Getting started\n\nHi.\n");

            DocsSiteBuilder.Build(new DocsSiteOptions
            {
                CorpusDirectory = corpus,
                OutputDirectory = output,
                Org = "Novolis-Platform",
            });

            var landing = await File.ReadAllTextAsync(Path.Combine(output, "novolis-bare", "index.html"));
            await Assert.That(landing).Contains("generated overview");
            await Assert.That(landing).Contains("getting-started.html");
        }
        finally
        {
            if (Directory.Exists(corpus))
            {
                Directory.Delete(corpus, recursive: true);
            }

            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }
}
