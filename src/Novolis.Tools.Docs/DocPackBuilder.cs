using Novolis.Tools.Docs.Graph;
using Novolis.Tools.Docs.Markdown;
using Novolis.Tools.Docs.Mermaid;

namespace Novolis.Tools.Docs;

/// <summary>A set of Markdown documents written as a documentation pack.</summary>
public sealed class DocPack
{
    private readonly List<MarkdownDocument> _documents = [];

    /// <summary>Logical pack title.</summary>
    public required string Title { get; init; }

    /// <summary>Documents in write order.</summary>
    public IReadOnlyList<MarkdownDocument> Documents => _documents;

    /// <summary>Adds a document.</summary>
    public DocPack Add(MarkdownDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _documents.Add(document);
        return this;
    }

    /// <summary>Writes all documents under <paramref name="outputDirectory"/> as <c>*.md</c> files.</summary>
    public IReadOnlyList<string> WriteTo(string outputDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        var written = new List<string>();
        foreach (var doc in _documents)
        {
            var fileName = Slug(doc.Title) + ".md";
            var path = Path.Combine(outputDirectory, fileName);
            File.WriteAllText(path, doc.ToMarkdown());
            written.Add(path);
        }

        return written;
    }

    private static string Slug(string title)
    {
        var chars = title.Trim().ToLowerInvariant().Select(ch =>
            char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }
}

/// <summary>Builds Markdown doc packs with Mermaid relationship graphs.</summary>
public static class DocPackBuilder
{
    /// <summary>Creates a starter documentation pack (overview / architecture / relationships / glossary).</summary>
    public static DocPack Scaffold(string title, string? summary = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var pack = new DocPack { Title = title };

        pack.Add(new MarkdownDocument("Overview")
            .H1(title)
            .P(summary ?? $"{title} documentation pack. Prefer Markdown with Mermaid for every structural claim.")
            .H2("Map")
            .Mermaid(MermaidDiagram.Mindmap(m => m
                .Root(title)
                .Node(1, "Overview")
                .Node(1, "Architecture")
                .Node(2, "Layers")
                .Node(2, "Boundaries")
                .Node(1, "Relationships")
                .Node(2, "Projects")
                .Node(2, "Packages")
                .Node(1, "Glossary")))
            .H2("Documents")
            .Bullets(
                "[architecture.md](architecture.md) — layer and boundary Mermaid",
                "[relationships.md](relationships.md) — project / package edges",
                "[glossary.md](glossary.md) — terms"));

        pack.Add(new MarkdownDocument("Architecture")
            .H1("Architecture")
            .P("Describe the closed spine and any orthogonal islands. Every edge below should be justified in prose.")
            .H2("Layer flowchart")
            .Mermaid(MermaidDiagram.Flowchart(c => c
                .Direction(FlowDirection.TB)
                .Node("math", "Math")
                .Node("physics", "Physics")
                .Node("simulation", "Simulation")
                .Node("gaming", "Gaming")
                .Node("avalonia", "Avalonia")
                .Node("apps", "Apps")
                .Edge("math", "physics")
                .Edge("physics", "simulation")
                .Edge("simulation", "gaming")
                .Edge("gaming", "avalonia")
                .Edge("avalonia", "apps")))
            .H2("Type sketch")
            .Mermaid(MermaidDiagram.ClassDiagram(c => c
                .Class("DocPack", "+Title", "+WriteTo()")
                .Class("RelationshipGraph", "+AddNode()", "+AddEdge()", "+ToMermaidFlowchart()")
                .Class("MarkdownDocument", "+H1()", "+Mermaid()")
                .Composes("DocPack", "MarkdownDocument")
                .Depends("DocPack", "RelationshipGraph")))
            .H2("Data sketch")
            .Mermaid(MermaidDiagram.ErDiagram(e => e
                .Entity("Document", "string title PK", "string body")
                .Entity("Node", "string id PK", "string label", "string kind")
                .Entity("Edge", "string from_id FK", "string to_id FK", "string label")
                .Relates("Document", "Node", "describes")
                .Relates("Node", "Edge", "has"))));

        pack.Add(new MarkdownDocument("Relationships")
            .H1("Relationships")
            .P("Replace this stub by running `novolis-docs graph` against a repo root, or edit the Mermaid by hand.")
            .H2("Dependency graph")
            .Mermaid(MermaidDiagram.Flowchart(c => c
                .Direction(FlowDirection.LR)
                .Node("cli", "Docs.Cli", "stadium")
                .Node("docs", "Tools.Docs")
                .Node("sqliteCli", "Sqlite.Cli", "stadium")
                .Node("sqlite", "Tools.Sqlite")
                .Node("liteCli", "LiteDb.Cli", "stadium")
                .Node("lite", "Tools.LiteDb")
                .Depends("cli", "docs", "ProjectReference")
                .Depends("sqliteCli", "sqlite", "ProjectReference")
                .Depends("liteCli", "lite", "ProjectReference")))
            .H2("Edge table")
            .Table(
                ["From", "To", "Kind"],
                [
                    ["Novolis.Tools.Docs.Cli", "Novolis.Tools.Docs", "ProjectReference"],
                    ["Novolis.Tools.Sqlite.Cli", "Novolis.Tools.Sqlite", "ProjectReference"],
                    ["Novolis.Tools.LiteDb.Cli", "Novolis.Tools.LiteDb", "ProjectReference"],
                ]));

        pack.Add(new MarkdownDocument("Glossary")
            .H1("Glossary")
            .Table(
                ["Term", "Meaning"],
                [
                    ["Doc pack", "A folder of Markdown files generated together"],
                    ["Relationship graph", "Nodes + directed edges rendered as Mermaid"],
                    ["Mermaid", "Diagram source embedded in fenced ```mermaid blocks"],
                    ["Storage-aligned tool", "A Tools package that depends on the matching Novolis.Storage.* engine"],
                ])
            .Quote("Prefer a Mermaid diagram over an ASCII sketch whenever structure matters."));

        return pack;
    }

    /// <summary>Builds a documentation pack from a scanned <see cref="RelationshipGraph"/>.</summary>
    public static DocPack FromGraph(string title, RelationshipGraph graph, string? summary = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(graph);

        var pack = new DocPack { Title = title };
        var projectCount = graph.Nodes.Count(n => n.Kind == GraphNodeKind.Project);
        var packageCount = graph.Nodes.Count(n => n.Kind == GraphNodeKind.Package);

        pack.Add(new MarkdownDocument("Overview")
            .H1(title)
            .P(summary ?? $"Generated documentation pack for **{title}**.")
            .H2("Inventory")
            .Bullets(
                $"{projectCount} projects",
                $"{packageCount} packages",
                $"{graph.Edges.Count} edges")
            .H2("Map")
            .Mermaid(graph.ToMermaidFlowchart(FlowDirectionHint.TB))
            .H2("Documents")
            .Bullets(
                "[architecture.md](architecture.md)",
                "[relationships.md](relationships.md)"));

        pack.Add(new MarkdownDocument("Architecture")
            .H1("Architecture")
            .P("Project nodes are rectangles; package nodes are stadiums; dashed edges are PackageReference.")
            .H2("Full graph")
            .Mermaid(graph.ToMermaidFlowchart(FlowDirectionHint.LR))
            .H2("Project-only mindmap")
            .Mermaid(MermaidDiagram.Mindmap(m =>
            {
                m.Root(title);
                foreach (var node in graph.Nodes.Where(n => n.Kind == GraphNodeKind.Project).Take(40))
                {
                    m.Node(1, node.Label);
                }
            })));

        pack.Add(new MarkdownDocument("Relationships")
            .H1("Relationships")
            .P("Directed edges discovered from `.csproj` references.")
            .H2("Graph")
            .Mermaid(graph.ToMermaidFlowchart(FlowDirectionHint.LR))
            .H2("Edges")
            .Table(["From", "To", "Kind"], graph.ToEdgeTableRows()));

        return pack;
    }
}
