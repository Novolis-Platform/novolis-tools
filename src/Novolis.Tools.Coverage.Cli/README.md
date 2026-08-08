# Novolis.Tools.Coverage.Cli

`novolis-coverage` — collect MTP Cobertura across Novolis repos / `Novolis.Platform.slnx`
and merge an HTML report (ReportGenerator).

## Install

```powershell
dotnet tool install --global Novolis.Tools.Coverage.Cli --version 2026.1.*
```

Requires .NET 10 SDK and `reportgenerator` (auto-installed on first run if missing):

```powershell
dotnet tool install -g dotnet-reportgenerator-globaltool
```

## Quick start

```powershell
# Platform.slnx + ProjectRef (fast path when already built)
novolis-coverage collect --platform --skip-build --fail-below -1 --out d:\novolis\coverage

# List hosts that would run
novolis-coverage list --platform

# Subset
novolis-coverage collect --platform --include novolis-math,novolis-io --out d:\temp\cov
```

Local run without installing:

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Coverage.Cli -- collect --platform --skip-build --fail-below -1 --out d:\novolis\coverage
```

HTML entry: `<out>/index.html` when `--flatten` is on (default), else `<out>/report/index.html`.

## Options

| Flag | Meaning |
|------|---------|
| `--root` | Workspace root (`NOVOLIS_ROOT` / walk from cwd) |
| `--out` | Output dir (default `<root>/coverage`) |
| `--platform` | Use `Novolis.Platform.slnx` + ProjectReference mode |
| `--regenerate-slnx` | Run `Generate-Platform-Slnx.ps1` first |
| `--skip-build` | Pass `--no-build` to `dotnet test` |
| `--fail-below` | Gate on aggregate line % (`-1` disables; Platform `0` → 95) |
| `--throttle` | Max parallel repos |
| `--exclude` / `--include` | Repo filters |
| `--flatten` | Place `index.html` at `--out` root (default true) |
| `--open` | Open the HTML report (Windows) |

Governance PowerShell (`get-coverage-report.ps1`) remains for CI; this tool is the local/agent entrypoint.
