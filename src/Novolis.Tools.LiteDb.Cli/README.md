# Novolis.Tools.LiteDb.Cli

`novolis-litedb` — interactive LiteDB shell REPL over the same engine as `Novolis.Storage.LiteDb`.

Accepts LiteDB’s SQL-like shell grammar (`SELECT $ FROM …`, `INSERT INTO … VALUES {…}`, and the rest). Dot-commands cover collections, indexes, and output mode. The library behind the tool is `Novolis.Tools.LiteDb`.

## Install

```powershell
dotnet tool install --global Novolis.Tools.LiteDb.Cli --version 2026.1.*
```

## Usage

```powershell
novolis-litedb d:\temp\app.db
novolis-litedb :memory: -c "INSERT INTO t VALUES {_id: 1}; SELECT $ FROM t;"
novolis-litedb d:\temp\app.db -c ".collections"
novolis-litedb d:\temp\secret.db -p hunter2 --mode json -c "SELECT $ FROM notes"
```

### Dot commands

| Command | Meaning |
|---------|---------|
| `.help` | Show help |
| `.collections` | List collections |
| `.indexes [collection]` | Show indexes from `$indexes` |
| `.mode table\|csv\|json` | Output format |
| `.quit` / `.exit` | Leave the REPL |

## Local run

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.LiteDb.Cli -- :memory: -c "SELECT 1 AS n;"
```

## Related

| Package | Role |
|---------|------|
| `Novolis.Tools.LiteDb` | Session helpers used by this tool |
| `Novolis.Tools.Sqlite.Cli` | `novolis-sqlite` for relational SQL |
| `Novolis.Storage.LiteDb` | Application repository provider |
