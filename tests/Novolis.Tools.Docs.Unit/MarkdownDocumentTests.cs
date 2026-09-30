using Novolis.Tools.Docs;
using Novolis.Tools.Docs.Graph;
using Novolis.Tools.Docs.Markdown;
using Novolis.Tools.Docs.Marketing;
using Novolis.Tools.Docs.Mermaid;
using Novolis.Tools.Docs.Seed;
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
