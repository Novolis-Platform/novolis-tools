<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-tools">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Tools.Sqlite.Cli

`novolis-sqlite` — Spectre SQLite REPL with pit-of-success defaults.

## Install

```powershell
dotnet tool install --global Novolis.Tools.Sqlite.Cli --version 2026.1.*
```

## Pit of success

| Default | Why |
|---------|-----|
| Refuse missing files | Pass `--create` to make a new DB (no accidental empty files) |
| `--read-only` | Inspect without write risk |
| Row limit **200** | `.limit 0` for all; prevents terminal floods |
| Confirm DROP/DELETE/ALTER/VACUUM | `.confirm off` or `--no-confirm` to skip |
| `PRAGMA foreign_keys=ON` | Integrity by default |
| Spectre tables | `--mode csv\|json` for piping |

## Examples

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Sqlite.Cli -- app.db --read-only
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Sqlite.Cli -- app.db --create
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Sqlite.Cli -- :memory: -c "SELECT 1 AS n;"
```

Inside the REPL: `.help`, `.help tables`, `.info`, `.export out.csv`.

