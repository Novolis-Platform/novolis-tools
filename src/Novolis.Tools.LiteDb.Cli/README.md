# Novolis.Tools.LiteDb.Cli

`novolis-litedb` — Spectre LiteDB shell REPL with pit-of-success defaults.

## Install

```powershell
dotnet tool install --global Novolis.Tools.LiteDb.Cli --version 2026.1.*
```

## Pit of success

| Default | Why |
|---------|-----|
| Refuse missing files | Pass `--create` to make a new DB |
| `--read-only` | Inspect without write risk |
| Document limit **200** | `.limit 0` for all |
| Confirm destructive shell SQL | `.confirm off` or `--no-confirm` |
| Spectre tables | `--mode csv\|json` for piping |

## Examples

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.LiteDb.Cli -- app.db --read-only
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.LiteDb.Cli -- app.db --create
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.LiteDb.Cli -- :memory: -c ".collections"
```

Inside the REPL: `.help`, `.help collections`, `.info`, `.export out.json`.
