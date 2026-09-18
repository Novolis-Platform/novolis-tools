<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-tools">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Tools.Docs

Markdown-first documentation builders with **Mermaid** relationship graphs.

## Install

```powershell
dotnet add package Novolis.Tools.Docs --version 2026.1.*
```

Source: GitHub Packages (`https://nuget.pkg.github.com/Novolis-Platform/index.json`).

## Quick start

```csharp
using Novolis.Tools.Docs.Markdown;
using Novolis.Tools.Docs.Mermaid;
using Novolis.Tools.Docs.Graph;

var md = new MarkdownDocument("Architecture")
    .H1("Architecture")
    .P("Layer edges are downward only.")
    .Mermaid(MermaidDiagram.Flowchart(chart => chart
        .Direction(FlowDirection.TB)
        .Node("math", "Math")
        .Node("physics", "Physics")
        .Edge("math", "physics")));

File.WriteAllText("architecture.md", md.ToMarkdown());
```

Scan a tree of `.csproj` files:

```csharp
var graph = ProjectGraphScanner.Scan(@"d:\novolis\novolis-tools");
var pack = DocPackBuilder.FromGraph("novolis-tools", graph);
pack.WriteTo(@"d:\temp\docs-out");
```

## API

| Type | Role |
|------|------|
| `MarkdownDocument` | Fluent Markdown document (headings, tables, fences, Mermaid blocks) |
| `MermaidDiagram` | Flowchart / class / ER / mindmap builders |
| `RelationshipGraph` | Nodes + edges → Mermaid + Markdown sections |
| `ProjectGraphScanner` | Walk `.csproj` PackageReference / ProjectReference edges |
| `DocPackBuilder` | Emit a Markdown doc pack (`overview`, `architecture`, `relationships`, …) |
| `DocsCorpusScanner` / `DocsSiteBuilder` | Scan `{repo}/docs/**/*.md` corpora and emit a static HTML site via Novolis.Markup |

```csharp
using Novolis.Tools.Docs.Site;

var count = DocsSiteBuilder.Build(new DocsSiteOptions
{
    CorpusDirectory = @"d:\novolis\.github\corpus",
    OutputDirectory = @"d:\novolis\.github\_site",
    AssetsDirectory = @"d:\novolis\.github\site\assets",
    BrandDirectory = @"d:\novolis\.github\brand",
});
```

See [docs/design.md](../../docs/design.md).

