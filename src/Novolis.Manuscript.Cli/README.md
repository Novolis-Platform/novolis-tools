<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-tools">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Manuscript.Cli

Command-line tool for NMP/1 manuscript workspaces: chapter surgery, audiobook generation, print, and metrics.

## Install

Install the CLI tool from GitHub Packages.

## Usage

```powershell
dotnet tool install -g Novolis.Manuscript.Cli --add-source https://nuget.pkg.github.com/Novolis-Platform/index.json

novolis-manuscript book list-books --workspace D:\repos\books
novolis-manuscript book doctor --series the-calypso-cycle --book calypso
novolis-manuscript book ascii-normalize --series the-calypso-cycle --book calypso --dry-run
novolis-manuscript book character-slices --series the-calypso-cycle --book calypso
novolis-manuscript audio --series the-calypso-cycle --book calypso --dry-run
novolis-manuscript print --series the-calypso-cycle --book calypso
novolis-manuscript metrics --workspace D:\repos\books
```

Local host:

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Manuscript.Cli\Novolis.Manuscript.Cli.csproj -- book list-books --workspace D:\repos\books
```

