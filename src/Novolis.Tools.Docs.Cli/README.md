# Novolis.Tools.Docs.Cli

`novolis-docs` — scaffold Markdown documentation packs and emit Mermaid relationship graphs from `.csproj` trees.

## Install

```powershell
dotnet tool install --global Novolis.Tools.Docs.Cli --version 2026.1.*
```

## Commands

```powershell
novolis-docs scaffold --title "FreightWing" --out d:\temp\fw-docs
novolis-docs graph --root d:\novolis\novolis-tools --out d:\temp\tools-graph
novolis-docs graph --root d:\novolis\novolis-tools --out d:\temp\tools-graph --novolis-packages-only
```

| Command | Purpose |
|---------|---------|
| `scaffold` | Write overview / architecture / relationships / glossary with Mermaid stubs |
| `graph` | Scan `.csproj` ProjectReference + PackageReference edges into a Markdown pack |

## Local run

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Docs.Cli -- scaffold --title Demo --out d:\temp\demo-docs
```
