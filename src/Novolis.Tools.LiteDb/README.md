# Novolis.Tools.LiteDb

Session helpers for ad-hoc LiteDB inspection — the same engine and connection-string conventions as `Novolis.Storage.LiteDb`, without the repository abstractions.

Use this when you need a small shell or script surface over a document database file that storage providers already own. Prefer `Novolis.Storage.LiteDb` when you want `IRepository<T>` in an application host.

## Install

```powershell
dotnet add package Novolis.Tools.LiteDb --version 2026.1.*
```

**Prerequisites:** .NET 10 (`net10.0`). Depends on **Novolis.Storage.LiteDb** (and transitively **LiteDB**).

Source: GitHub Packages (`https://nuget.pkg.github.com/Novolis-Platform/index.json`).

## Quick start

```csharp
using Novolis.Storage.LiteDb;
using Novolis.Tools.LiteDb;

using var session = LiteDbSession.Open("app.db");
foreach (var name in session.ListCollections())
{
    Console.WriteLine(name);
}

session.Execute("INSERT INTO items VALUES {_id: 1, name: \"alpha\"}");
var result = session.Execute("SELECT $ FROM items");
Console.WriteLine(result.ToTable());

// Same options shape as AddLiteDbProvider:
using var fromOptions = LiteDbSession.Open(new LiteDbOptions
{
    DatabasePath = "app.db",
});
```

File opens add `Connection=shared` (matching storage) so tools and hosts can coexist on the same path. Use `:memory:` for an in-process database.

## API

| Type | Role |
|------|------|
| `LiteDbSession` | Open file / `:memory:` / `LiteDbOptions`; wrap an existing `ILiteDatabase`; list collections; dump indexes; execute shell SQL |
| `LiteDbQueryResult` | Flattened columns + rows with `ToTable` / `ToCsv` / `ToJsonLines` |

## Related

| Package | Role |
|---------|------|
| `Novolis.Tools.LiteDb.Cli` | `novolis-litedb` interactive REPL |
| `Novolis.Storage.LiteDb` | `IRepository<T>` provider for app hosts |
| `Novolis.Tools.Sqlite` | Parallel helpers for SQLite files |

Interactive use: install `Novolis.Tools.LiteDb.Cli` and run `novolis-litedb`.

## Docs

- [Getting started](https://github.com/Novolis-Platform/novolis-tools/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-tools/blob/main/docs/design.md)
