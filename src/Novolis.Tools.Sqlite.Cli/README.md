# Novolis.Tools.Sqlite.Cli

`novolis-sqlite` — interactive SQLite REPL over the same engine as `Novolis.Storage.Sqlite`.

Dot-commands mirror the familiar `sqlite3` surface (`.tables`, `.schema`, `.mode`) without shipping the native binary. The library behind the tool is `Novolis.Tools.Sqlite`.

## Install

```powershell
dotnet tool install --global Novolis.Tools.Sqlite.Cli --version 2026.1.*
```

## Usage

```powershell
novolis-sqlite d:\temp\app.db
novolis-sqlite :memory: -c "CREATE TABLE t(id INTEGER); SELECT * FROM t;"
novolis-sqlite d:\temp\app.db -c ".tables"
```

### Dot commands

| Command | Meaning |
|---------|---------|
| `.help` | Show help |
| `.tables` | List user tables |
| `.schema [table]` | Show CREATE SQL |
| `.mode table\|csv` | Output format |
| `.quit` / `.exit` | Leave the REPL |

## Local run

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Sqlite.Cli -- :memory: -c "SELECT 1 AS n;"
```

## Related

| Package | Role |
|---------|------|
| `Novolis.Tools.Sqlite` | Session helpers used by this tool |
| `Novolis.Tools.LiteDb.Cli` | `novolis-litedb` for LiteDB shell SQL |
| `Novolis.Storage.Sqlite` | Application repository provider |
