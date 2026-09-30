# Novolis.Tools.CodeLayout.Cli

`novolis-code-layout` maps and repairs C# source layout across an entire `.slnx`.

## Install

```powershell
dotnet tool install --global Novolis.Tools.CodeLayout.Cli --add-source https://nuget.pkg.github.com/Novolis-Platform/index.json
```

## Map a solution

```powershell
novolis-code-layout map d:\novolis\Novolis.Platform.slnx
novolis-code-layout map d:\novolis\Novolis.Platform.slnx --json
```

The map reports:

- files with more than one distinct top-level type, including the type kept and the types to move;
- type-less files, including top-level-statement, using-only, metadata-only, directive-only, empty-namespace, and empty/comment-only files;
- safe deletion candidates;
- generated files skipped from the default scan.

The input path may also be used without the `map` verb:

```powershell
novolis-code-layout d:\novolis\Novolis.Platform.slnx
novolis-code-layout d:\novolis\Novolis.Platform.slnx --delete
```

## Repair a solution

```powershell
novolis-code-layout fix d:\novolis\Novolis.Platform.slnx
novolis-code-layout fix d:\novolis\Novolis.Platform.slnx --delete
```

`fix` moves extra top-level types into new files beside their source file. It keeps
the type whose name matches the source file, or the first type when no name matches.
The fixer preserves regular usings and namespaces, and leaves global usings and
assembly attributes on the original file.

`--delete` is required before any deletion. It only removes files classified as
empty or comment-only with no types, statements, usings, attributes, extern aliases,
or significant preprocessor directives. Type-less files that may carry imports,
metadata, top-level statements, or directives are mapped but retained. Files shared
by more than one project and generated files are never deletion candidates.

Use `--include-generated` to map generated files for inspection. Generated files
are never rewritten or deleted by `fix`.
