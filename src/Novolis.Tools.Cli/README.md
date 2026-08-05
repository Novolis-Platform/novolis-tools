# Novolis.Tools.Cli

Shared Spectre.Console chrome for Novolis developer CLIs: result tables, help panels, REPL prompts, and pit-of-success defaults (row limits, read-only open, destructive confirms).

## Install

```powershell
dotnet add package Novolis.Tools.Cli --version 2026.1.*
```

## API

| Type | Role |
|------|------|
| `OutputMode` | `table` / `csv` / `json` |
| `TabularResult` | Columns + rows + unit label |
| `ResultPrinter` | Spectre table / CSV / JSON / file export |
| `ReplChrome` | Banner, prompt, errors, status |
| `DotHelp` | Rich `.help` catalog |
| `OpenGuards` | Create-vs-existing + read-only path rules |
