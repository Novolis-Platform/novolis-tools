<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-tools">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Tools.Coverage.Cli

`novolis-coverage` — collect MTP Cobertura across Novolis repos / `Novolis.Platform.slnx`,
merge HTML reports, analyze package gaps, and write a single CRAP risk markdown file.

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

# CRAP report → one file under the caller's cwd (default ./CRAP.md)
# Discovers Platform.slnx Cobertura set and scores in parallel
novolis-coverage crap --fail-above -1

# Explicit report path + coverage dir from collect --platform
novolis-coverage crap --out d:\novolis\CRAP.md --coverage-dir d:\novolis\coverage --fail-above -1
```

Local run without installing:

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Coverage.Cli -p:NovolisUseProjectReferences=true -- collect --platform --skip-build --fail-below -1 --out d:\novolis\coverage
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.Coverage.Cli -p:NovolisUseProjectReferences=true -- crap --out d:\novolis\CRAP.md --fail-above -1
```

HTML entry: `<workspace>/COVERAGE.html` (single-file summary + risk hotspots, next to Platform.slnx). Full drill-down under `<out>/` when `--flatten` is on.

## Options

| Flag | Meaning |
|------|---------|
| `--root` | Workspace root (`NOVOLIS_ROOT` / walk from cwd / `Novolis.Platform.slnx`) |
| `--out` | Collect: output dir. `crap`: single report file (default `./CRAP.md`) |
| `--platform` | Collect/list: use `Novolis.Platform.slnx` + ProjectReference mode |
| `--platform-slnx` | Explicit `Novolis.Platform.slnx` path (`crap` always platform-scoped) |
| `--coverage-dir` | `crap`: coverage root from `collect --platform` (default `<root>/coverage`) |
| `--regenerate-slnx` | Run `Generate-Platform-Slnx.ps1` first |
| `--skip-build` | Pass `--no-build` to `dotnet test` |
| `--fail-below` | Gate on aggregate line % (`-1` disables; Platform `0` → 95) |
| `--threshold` / `--fail-above` | CRAP flag / fail gate (default 30; `-1` disables fail) |
| `--all` | CRAP table includes non-flagged methods (default: flagged only) |
| `--throttle` | Max parallel repos / Cobertura parses |
| `--exclude` / `--include` | Repo filters |
| `--flatten` | Place `index.html` at `--out` root (default true) |
| `--open` | Open the HTML report (Windows) |

Governance PowerShell (`get-coverage-report.ps1`) remains for CI; this tool is the local/agent entrypoint.

