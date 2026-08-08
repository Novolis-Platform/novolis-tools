# Release

CalVer `YEAR.MAJOR.MINOR.BUILD` via `build/version.json` and merge CI (`github.run_number` as BUILD).

- **Merge to `main`** (package-surface changes) → pack + push to GitHub Packages
- **GitHub Release** → nuget.org (workflow `release.yml`)

See [release policy](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/release-policy.md).

Consumer float for packages in this repo: **`2026.1.*`**.

## Packages

| Package | Kind |
|---------|------|
| `Novolis.Tools.Cli` | Library (Spectre helpers) |
| `Novolis.Tools.Coverage` | Library (coverage collect / merge) |
| `Novolis.Tools.Coverage.Cli` | `dotnet tool` (`novolis-coverage`) |
| `Novolis.Tools.Docs` | Library |
| `Novolis.Tools.Docs.Cli` | `dotnet tool` (`novolis-docs`) |
| `Novolis.Tools.Sqlite` | Library (depends on `Novolis.Storage.Sqlite`) |
| `Novolis.Tools.Sqlite.Cli` | `dotnet tool` (`novolis-sqlite`) |
| `Novolis.Tools.LiteDb` | Library (depends on `Novolis.Storage.LiteDb`) |
| `Novolis.Tools.LiteDb.Cli` | `dotnet tool` (`novolis-litedb`) |
