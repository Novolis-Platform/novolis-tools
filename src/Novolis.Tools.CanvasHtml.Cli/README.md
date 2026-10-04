# Novolis.Tools.CanvasHtml.Cli

Render Cursor `.canvas.tsx` files to self-contained HTML.

## Install

```powershell
dotnet tool install --global Novolis.Tools.CanvasHtml.Cli --add-source https://nuget.pkg.github.com/Novolis-Platform/index.json
```

## Quick start

```powershell
novolis-canvas-html D:\repos\books\src\Fiction\galactic-confederation\the-calypso-cycle\References\ships\calypso\other
```

Each `name.canvas.tsx` becomes `name.html` in the same directory. Pass `--out` to write the HTML somewhere else. The page inlines its CSS and does not load a network.

`--combine` writes one tabbed document instead:

```powershell
novolis-canvas-html D:\repos\books\src\Fiction\galactic-confederation\the-calypso-cycle\References\ships\calypso\other --combine D:\repos\books\src\Fiction\galactic-confederation\the-calypso-cycle\References\ships\calypso\other\calypso-canvases.html
```
