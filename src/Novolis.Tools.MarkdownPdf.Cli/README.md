<!-- novolis-pkg-brand:start -->
<p align="center">
  <a href="https://github.com/Novolis-Platform/novolis-tools">
    <img src="https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.svg" width="72" alt="Novolis"/>
  </a>
</p>
<!-- novolis-pkg-brand:end -->

# Novolis.Tools.MarkdownPdf.Cli

`novolis-mdpdf` — convert Markdown to PDF with named themes (`trade`, `report`, `compact`, `plain`).

## Install

```powershell
dotnet tool install --global Novolis.Tools.MarkdownPdf.Cli --version 2026.1.*
```

## Quick start

## Commands

```powershell
novolis-mdpdf themes

novolis-mdpdf convert --in D:\repos\books\out\the-calypso-cycle\calypso\calypso.md --theme trade --title "The Calypso Cycle"

novolis-mdpdf convert --in C:\Users\frank\.novolis\artifacts\calypso-documents-pdf\chapter-144-dress-uniform.md --out C:\Users\frank\.novolis\artifacts\mdpdf\chapter-144.pdf --theme trade
```

| Command | Purpose |
|---------|---------|
| `themes` | List built-in theme ids |
| `convert` | Markdown file → PDF (`--theme`, `--title`, `--author`, `--cover`, `--toc`) |

Default output (when `--out` omitted): `C:\Users\frank\.novolis\artifacts\mdpdf\<name>.pdf`

## Local run

```powershell
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.MarkdownPdf.Cli -- themes
dotnet run --project d:\novolis\novolis-tools\src\Novolis.Tools.MarkdownPdf.Cli -- convert --in D:\path\file.md --theme trade
```

## Support

- Library: [Novolis.Tools.MarkdownPdf](https://github.com/Novolis-Platform/novolis-tools/blob/main/src/Novolis.Tools.MarkdownPdf/README.md)
- Issues: [GitHub Issues](https://github.com/Novolis-Platform/novolis-tools/issues)
