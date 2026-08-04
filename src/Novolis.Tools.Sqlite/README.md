# Novolis.Tools.Sqlite

Thin session helpers over `Microsoft.Data.Sqlite` for REPL and scripting tools.

## Install

```powershell
dotnet add package Novolis.Tools.Sqlite --version 2026.1.*
```

## Quick start

```csharp
using Novolis.Tools.Sqlite;

await using var session = SqliteSession.Open("app.db");
foreach (var table in await session.ListTablesAsync())
{
    Console.WriteLine(table);
}

var result = await session.ExecuteAsync("SELECT 1 AS n");
Console.WriteLine(result.ToTable());
```

## API

| Type | Role |
|------|------|
| `SqliteSession` | Open file / `:memory:`, list tables, schema, execute SQL |
| `SqliteQueryResult` | Columns + rows with `ToTable` / `ToCsv` formatters |

Prefer the `novolis-sqlite` tool for interactive use.
