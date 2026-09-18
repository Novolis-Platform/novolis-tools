<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-tools">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Tools.Sqlite

Session helpers for ad-hoc SQLite inspection — the same engine stack as `Novolis.Storage.Sqlite`, without the repository abstractions.

Use this when you need a small REPL or script surface over a `.db` file that storage providers already own. Prefer `Novolis.Storage.Sqlite` when you want `IRepository<T>` in an application host.

## Install

```powershell
dotnet add package Novolis.Tools.Sqlite --version 2026.1.*
```

**Prerequisites:** .NET 10 (`net10.0`). Depends on **Novolis.Storage.Sqlite** (and transitively **Microsoft.Data.Sqlite**).

Source: GitHub Packages (`https://nuget.pkg.github.com/Novolis-Platform/index.json`).

## Quick start

```csharp
using Novolis.Storage.Sqlite;
using Novolis.Tools.Sqlite;

await using var session = SqliteSession.Open("app.db");
foreach (var table in await session.ListTablesAsync())
{
    Console.WriteLine(table);
}

var result = await session.ExecuteAsync("SELECT 1 AS n");
Console.WriteLine(result.ToTable());

// Same connection-string shape as AddSqliteProvider:
await using var fromOptions = SqliteSession.Open(new SqliteOptions
{
    ConnectionString = "Data Source=app.db",
});
```

## API

| Type | Role |
|------|------|
| `SqliteSession` | Open file / `:memory:` / `SqliteOptions`; list tables; dump DDL; execute SQL |
| `SqliteQueryResult` | Columns + rows with `ToTable` / `ToCsv` |

## Related

| Package | Role |
|---------|------|
| `Novolis.Tools.Sqlite.Cli` | `novolis-sqlite` interactive REPL |
| `Novolis.Storage.Sqlite` | `IRepository<T>` provider for app hosts |
| `Novolis.Tools.LiteDb` | Parallel helpers for LiteDB document files |

Interactive use: install `Novolis.Tools.Sqlite.Cli` and run `novolis-sqlite`.

## Docs

- [Getting started](https://github.com/Novolis-Platform/novolis-tools/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-tools/blob/main/docs/design.md)

