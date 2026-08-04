<!-- novolis-marketing:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-brand-transparent.svg" width="360" alt="Novolis"/>
  </a>
</p>

<p align="center">
  <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/banners/novolis-template-dotnet.svg" width="100%" alt="novolis-template-dotnet"/>
</p>

<p align="center">
  <strong>Canonical package repo template</strong><br/>
  Template for new Novolis .NET package repositories.
</p>

<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-template-dotnet/actions"><img src="https://img.shields.io/github/actions/workflow/status/Novolis-Platform/novolis-template-dotnet/merge.yml?branch=main&label=merge&logo=github" alt="merge"/></a>
  <a href="https://github.com/orgs/Novolis-Platform/packages?repo_name=novolis-template-dotnet"><img src="https://img.shields.io/badge/packages-GitHub%20Packages-0a7ea3?logo=nuget" alt="packages"/></a>
  <a href="https://github.com/Novolis-Platform"><img src="https://img.shields.io/badge/org-Novolis--Platform-111827" alt="org"/></a>
</p>

<p align="center">
  <a href="https://nuget.pkg.github.com/Novolis-Platform/index.json"><code>https://nuget.pkg.github.com/Novolis-Platform/index.json</code></a>
  ·
  <a href="https://github.com/Novolis-Platform/.github/blob/main/profile/README.md">Org landing</a>
  ·
  <a href="https://github.com/Novolis-Platform/novolis-governance">Governance</a>
</p>

---
<!-- novolis-marketing:end -->
# novolis-template-dotnet

Canonical GitHub **repository template** for new Novolis package, tool, analyzer, app, and template repos.

Use **[Use this template](https://github.com/Novolis-Platform/novolis-template-dotnet/generate)** on GitHub to create a new repository, then rename and add projects.

Supports: library, CLI tool, analyzer, game/app, and template repos without over-specializing.

## What you get

| Item | Purpose |
|------|---------|
| `Directory.Build.props` / `.targets` | `net10.0`, packable defaults, artifacts layout |
| `Directory.Packages.props` | Central package management (optional baseline) |
| `build/version.json` + `version.props` | CalVer versioning |
| `build/Novolis.Documentation.props` | README + XML doc policy for packable projects |
| `nuget.config` | nuget.org + GitHub Packages (`Novolis.*` at `2026.1.*`) |
| `.github/workflows/` | PR, merge, and release CI |
| `docs/getting-started.md` | Documentation defaults checklist |

## Quick start

```powershell
# After generating your repo from the template:
dotnet restore
dotnet build
```

Add your first project:

```powershell
dotnet new classlib -n Acme.Widgets.Core -o src/Acme.Widgets.Core
dotnet sln add src/Acme.Widgets.Core/Acme.Widgets.Core.csproj
```

## Documentation defaults

New packable projects should:

1. Import `build/Novolis.Documentation.props` (included via root `Directory.Build.props`).
2. Add `README.md` next to each packable `.csproj` with Install/API sections.
3. Document public API with XML comments before removing transitional `CS1591` suppressions.

See [documentation-policy.md](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/documentation-policy.md).

## Package sources

**Only** nuget.org and GitHub Packages. No local folder feeds. Cross-repo iteration on platform libraries: open **`Novolis.Platform.slnx`** (ProjectReference mode) — see [platform-project-ref-mode](https://github.com/Novolis-Platform/novolis-governance/blob/main/docs/platform-project-ref-mode.md).

## Related templates

For `dotnet new` scaffolds (microservice, Avalonia, MonoGame), use the **[novolis-templates](https://github.com/Novolis-Platform/novolis-templates)** package instead.

## More documentation

- [Getting started](docs/getting-started.md)
- [Design](docs/design.md)
- [Release](docs/release.md)

