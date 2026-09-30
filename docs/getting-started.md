# Getting started

## Prerequisites

- .NET SDK **10.0.100+** (`d:\novolis\novolis-tools\global.json`)
- NuGet sources: nuget.org + GitHub Packages for `Novolis.*`

## Build and test

```powershell
dotnet restore d:\novolis\novolis-tools\Novolis.Tools.slnx
dotnet build d:\novolis\novolis-tools\Novolis.Tools.slnx
dotnet test d:\novolis\novolis-tools\Novolis.Tools.slnx
```

Local iteration against sibling Storage sources (no GPR publish wait):

```powershell
dotnet build d:\novolis\novolis-tools\Novolis.Tools.slnx -p:NovolisUseProjectReferences=true
dotnet test d:\novolis\novolis-tools\Novolis.Tools.slnx -p:NovolisUseProjectReferences=true
```

## Tools

### novolis-docs

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Docs.Cli -- scaffold --title "My Feature" --out d:\temp\my-docs
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Docs.Cli -- graph --root d:\novolis\novolis-tools --out d:\temp\tools-graph
```

After publish to GitHub Packages:

```powershell
dotnet tool install --global Novolis.Tools.Docs.Cli --version 2026.1.*
novolis-docs scaffold --title "My Feature" --out d:\temp\my-docs
```

### novolis-coverage

Platform / org Cobertura collection + HTML merge (ReportGenerator). Prefer this over the governance PowerShell script for local/agent runs.

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Coverage.Cli -- list --platform
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Coverage.Cli -- collect --platform --skip-build --fail-below -1 --out d:\novolis\coverage
```

```powershell
dotnet tool install --global Novolis.Tools.Coverage.Cli --version 2026.1.*
novolis-coverage collect --platform --skip-build --fail-below -1 --out d:\novolis\coverage
```

### novolis-code-layout

Map and repair C# source layout across an entire `.slnx`. The map identifies files
with multiple top-level types, type-less files, and safe deletion candidates.

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.CodeLayout.Cli -- map d:\novolis\Novolis.Platform.slnx
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.CodeLayout.Cli -- map d:\novolis\Novolis.Platform.slnx --json
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.CodeLayout.Cli -- fix d:\novolis\Novolis.Platform.slnx
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.CodeLayout.Cli -- fix d:\novolis\Novolis.Platform.slnx --delete
```

`fix` moves extra types into separate files. `--delete` is required to remove
only empty or comment-only candidates; files containing usings, attributes,
top-level statements, directives, generated content, or shared project references
are retained.

```powershell
dotnet tool install --global Novolis.Tools.CodeLayout.Cli --version 2026.1.*
novolis-code-layout map d:\novolis\Novolis.Platform.slnx
```

### novolis-sqlite

Spectre REPL over `Novolis.Storage.Sqlite`. Missing files are refused unless `--create`; prefer `--read-only` when inspecting.

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Sqlite.Cli -- :memory: -c "SELECT 1 AS n;"
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Sqlite.Cli -- d:\temp\app.db --read-only -c ".tables"
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Sqlite.Cli -- d:\temp\new.db --create
```

```powershell
dotnet tool install --global Novolis.Tools.Sqlite.Cli --version 2026.1.*
novolis-sqlite d:\temp\app.db --read-only
```

### novolis-litedb

Spectre LiteDB shell with the same pit-of-success defaults (`--create`, `--read-only`, row limits, destructive confirms):

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.LiteDb.Cli -- :memory: -c ".collections"
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.LiteDb.Cli -- d:\temp\app.db --create
```

```powershell
dotnet tool install --global Novolis.Tools.LiteDb.Cli --version 2026.1.*
novolis-litedb d:\temp\app.db --read-only
```

## Libraries

| Package | Use |
|---------|-----|
| `Novolis.Tools.Cli` | Spectre helpers for building more tools |
| `Novolis.Tools.Coverage` | Coverage collect / merge APIs |
| `Novolis.Tools.CodeLayout` | SLNX-wide C# source layout mapping and repair APIs |
| `Novolis.Tools.Docs` | Markdown / Mermaid / relationship graphs in code |
| `Novolis.Tools.MarkdownPdf` | Markdown → PDF with named themes |
| `Novolis.Tools.Sqlite` | SQLite session helpers (depends on `Novolis.Storage.Sqlite`) |
| `Novolis.Tools.LiteDb` | LiteDB session helpers (depends on `Novolis.Storage.LiteDb`) |

```powershell
dotnet add package Novolis.Tools.Docs --version 2026.1.*
dotnet add package Novolis.Tools.MarkdownPdf --version 2026.1.*
dotnet add package Novolis.Tools.Coverage --version 2026.1.*
dotnet add package Novolis.Tools.Sqlite --version 2026.1.*
dotnet add package Novolis.Tools.LiteDb --version 2026.1.*
```

When you need typed repositories in an app, reference the Storage packages directly. When you need to peek at the files those packages wrote, use these Tools packages (or their CLIs).
