# Novolis.Tools.Sqlite.Cli

`novolis-sqlite` — interactive SQLite REPL over `Microsoft.Data.Sqlite`.

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
