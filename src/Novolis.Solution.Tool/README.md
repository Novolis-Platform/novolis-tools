# Novolis.Solution.Tool

`novolis-solution` is a .NET tool for querying typed `.slnx` topology and catalog snapshots.

## Install

```powershell
dotnet tool install --global Novolis.Solution.Tool --add-source https://nuget.pkg.github.com/Novolis-Platform/index.json
```

## Quick start

```powershell
novolis-solution topology d:\novolis\novolis-workspaces\Novolis.Workspaces.slnx
novolis-solution catalog d:\novolis\novolis-workspaces\Novolis.Workspaces.slnx --allow-evaluation --json
```

Add `--allow-design-time-builds` only for a trusted source tree. The tool reports structured
diagnostics and exits unsuccessfully when an error diagnostic occurs.
