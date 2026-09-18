<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-tools">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Xsd.Tool

`dotnet tool` (`novolis-xsd`) that regenerates pre-generated sources from vendored XSDs.

The tool host lives in `novolis-tools`; pass the checked-out `novolis-xsd`
repository with `--repo` when generating source files.

## Quick start

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Xsd.Cli\Novolis.Xsd.Tool.csproj -- ubl --repo d:\novolis\novolis-xsd
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Xsd.Cli\Novolis.Xsd.Tool.csproj -- ubl-base --repo d:\novolis\novolis-xsd
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Xsd.Cli\Novolis.Xsd.Tool.csproj -- peppol --repo d:\novolis\novolis-xsd
```

## Install

```bash
dotnet add package Novolis.Xsd.Tool
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (net10.0).

