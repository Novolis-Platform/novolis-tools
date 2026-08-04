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

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Sqlite.Cli -- :memory: -c "SELECT 1 AS n;"
```

```powershell
dotnet tool install --global Novolis.Tools.Sqlite.Cli --version 2026.1.*
novolis-sqlite d:\temp\app.db
```

## Libraries

| Package | Use |
|---------|-----|
| `Novolis.Tools.Docs` | Markdown / Mermaid / relationship graphs in code |
| `Novolis.Tools.Sqlite` | SQLite session helpers |

```powershell
dotnet add package Novolis.Tools.Docs --version 2026.1.*
dotnet add package Novolis.Tools.Sqlite --version 2026.1.*
```
