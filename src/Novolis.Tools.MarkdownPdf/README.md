<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-tools">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Tools.MarkdownPdf

Markdown → PDF via `Novolis.Markup.Markdown.Documents` + `Novolis.Documents.Skia`, with named **themes** (trim, typography, header/footer, callout text box).

## Install

```bash
dotnet add package Novolis.Tools.MarkdownPdf
```

Requires .NET 10. Restore from nuget.org + GitHub Packages.

## Quick start

```csharp
using Novolis.Tools.MarkdownPdf;

MarkdownPdfConverter.ConvertFile(
    @"D:\path\chapter.md",
    @"C:\Users\frank\.novolis\artifacts\out.pdf",
    themeId: "trade",
    request: new MarkdownPdfConvertRequest { Title = "Sample", Author = "Novolis" });

foreach (var theme in MarkdownPdfThemes.All)
    Console.WriteLine($"{theme.Id}: {theme.Description}");
```

## Themes

| Id | Role |
|----|------|
| `trade` | 6×9, chapter-title header, callout text box (default) |
| `report` | A4 report margins, document-title header |
| `compact` | A5, smaller type |
| `plain` | 6×9, page numbers only |

## Related packages

| Package | When to use |
|---------|-------------|
| `Novolis.Tools.MarkdownPdf.Cli` | `novolis-mdpdf` global tool |
| `Novolis.Markup.Markdown.Documents` | Fluent / mapper APIs without themes |
| `Novolis.Documents.Skia` | Direct `PagedDocument` → PDF |

## Support

- Docs: [novolis-tools](https://github.com/Novolis-Platform/novolis-tools)
- Issues: [GitHub Issues](https://github.com/Novolis-Platform/novolis-tools/issues)
