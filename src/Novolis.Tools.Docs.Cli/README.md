<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-tools">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Tools.Docs.Cli

`novolis-docs` — scaffold Markdown documentation packs, emit Mermaid relationship graphs from `.csproj` trees, and build multi-repo `docs/` HTML sites.

## Install

```powershell
dotnet tool install --global Novolis.Tools.Docs.Cli --version 2026.1.*
```

## Quick start

## Commands

```powershell
novolis-docs scaffold --title "FreightWing" --out d:\temp\fw-docs
novolis-docs graph --root d:\novolis\novolis-tools --out d:\temp\tools-graph
novolis-docs graph --root d:\novolis\novolis-tools --out d:\temp\tools-graph --novolis-packages-only
novolis-docs site --corpus d:\novolis\.github\corpus --out d:\novolis\.github\_site --assets d:\novolis\.github\site\assets --brand d:\novolis\.github\brand
```

| Command | Purpose |
|---------|---------|
| `scaffold` | Write overview / architecture / relationships / glossary with Mermaid stubs |
| `graph` | Scan `.csproj` ProjectReference + PackageReference edges into a Markdown pack |
| `site` | Build a multi-page docs site from `{corpus}/{repo}/docs/**/*.md` (catalog + per-repo sidebar nav; Docs lands on `docs/README.md`) |

## Local run

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Docs.Cli -- scaffold --title Demo --out d:\temp\demo-docs
```

