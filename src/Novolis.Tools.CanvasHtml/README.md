# Novolis.Tools.CanvasHtml

Renders a Cursor `.canvas.tsx` file to one self-contained HTML document.

## Install

```bash
dotnet add package Novolis.Tools.CanvasHtml
```

## Quick start

```csharp
var html = CanvasHtmlRenderer.Render(File.ReadAllText("board.canvas.tsx"));
```

The renderer strips TypeScript and JSX, evaluates the canvas against a built-in `cursor/canvas` runtime, and inlines the CSS. No browser and no Node are required.

The command-line host is `Novolis.Tools.CanvasHtml.Cli` (`novolis-canvas-html`).
