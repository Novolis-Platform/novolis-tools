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

### novolis-sqlite

Inspect SQLite files with the same engine stack as `Novolis.Storage.Sqlite`:

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Sqlite.Cli -- :memory: -c "SELECT 1 AS n;"
```

```powershell
dotnet tool install --global Novolis.Tools.Sqlite.Cli --version 2026.1.*
novolis-sqlite d:\temp\app.db
```

### novolis-litedb

Inspect LiteDB files with the same engine and connection-string conventions as `Novolis.Storage.LiteDb`:

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.LiteDb.Cli -- :memory: -c "INSERT INTO t VALUES {_id: 1, name: \"ok\"}; SELECT $ FROM t;"
```

```powershell
dotnet tool install --global Novolis.Tools.LiteDb.Cli --version 2026.1.*
novolis-litedb d:\temp\app.db
```

## Libraries

| Package | Use |
|---------|-----|
| `Novolis.Tools.Docs` | Markdown / Mermaid / relationship graphs in code |
| `Novolis.Tools.Sqlite` | SQLite session helpers (depends on `Novolis.Storage.Sqlite`) |
| `Novolis.Tools.LiteDb` | LiteDB session helpers (depends on `Novolis.Storage.LiteDb`) |

```powershell
dotnet add package Novolis.Tools.Docs --version 2026.1.*
dotnet add package Novolis.Tools.Sqlite --version 2026.1.*
dotnet add package Novolis.Tools.LiteDb --version 2026.1.*
```

When you need typed repositories in an app, reference the Storage packages directly. When you need to peek at the files those packages wrote, use these Tools packages (or their CLIs).
