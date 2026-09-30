using Novolis.Tools.Docs;
using Novolis.Tools.Docs.Graph;
using Novolis.Tools.Docs.Markdown;
using Novolis.Tools.Docs.Marketing;
using Novolis.Tools.Docs.Mermaid;
using Novolis.Tools.Docs.Seed;
using Novolis.Tools.Docs.Site;

namespace Novolis.Tools.Docs.Unit;

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
