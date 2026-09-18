<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-tools">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Tools.Cli

Shared Spectre.Console chrome for Novolis developer CLIs: result tables, help panels, REPL prompts, and pit-of-success defaults (row limits, read-only open, destructive confirms).

## Install

```powershell
dotnet add package Novolis.Tools.Cli --version 2026.1.*
```

## Quick start

Use `ResultPrinter` and `DotHelp` in a command host to provide consistent table, JSON, and help output.

## API

| Type | Role |
|------|------|
| `OutputMode` | `table` / `csv` / `json` |
| `TabularResult` | Columns + rows + unit label |
| `ResultPrinter` | Spectre table / CSV / JSON / file export |
| `ReplChrome` | Banner, prompt, errors, status |
| `DotHelp` | Rich `.help` catalog |
| `OpenGuards` | Create-vs-existing + read-only path rules |

