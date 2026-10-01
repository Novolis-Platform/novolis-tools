<!-- novolis-pkg-brand:start -->
[![Novolis](https://raw.githubusercontent.com/Novolis-Platform/.github/main/brand/logo-icon.png)](https://novolis-platform.github.io/.github/novolis-tools/)

[Novolis](https://github.com/Novolis-Platform) · [Docs](https://novolis-platform.github.io/.github/novolis-tools/) · [Source](https://github.com/Novolis-Platform/novolis-tools)
<!-- novolis-pkg-brand:end -->

# Novolis.Install

Cross-platform Novolis package installer and launcher distributed as a .NET global tool (`novolis`).

## Install

```bash
dotnet tool install -g Novolis.Install
```

**Prerequisites:** [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0`).

## Quick start

```bash
novolis search
novolis doctor
novolis install <package-id> --channel stable
```

## Related packages

| Package | When to use |
|---------|-------------|
| *(none)* | CLI tool; not referenced as a library dependency. |

## More documentation

- [Getting started](https://github.com/Novolis-Platform/novolis-tools/blob/main/docs/getting-started.md)
- [Design](https://github.com/Novolis-Platform/novolis-tools/blob/main/docs/design.md)

## Support

Early-stage installer; several commands are stubs. Paths and registry index loading are implemented.

