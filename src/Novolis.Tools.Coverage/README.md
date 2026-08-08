# Novolis.Tools.Coverage

Library helpers to collect Microsoft Testing Platform Cobertura output across Novolis
test hosts and merge HTML via ReportGenerator.

## Install

```powershell
dotnet add package Novolis.Tools.Coverage --version 2026.1.*
```

## Usage

```csharp
using Novolis.Tools.Coverage;

var root = CoverageWorkspace.ResolveRoot();
var result = await new CoverageCollector(Console.Out).CollectAsync(new CoverageCollectOptions
{
    Root = root,
    OutputDir = Path.Combine(root, "coverage"),
    PlatformSlnx = true,
    SkipBuild = true,
    Configuration = "Debug",
    FailBelow = -1,
    FlattenHtml = true,
});

Console.WriteLine(result.HtmlIndexPath);
Console.WriteLine($"line={result.AggregateLinePercent}% branch={result.AggregateBranchPercent}%");
```

Prefer the CLI package for day-to-day use: `Novolis.Tools.Coverage.Cli` (`novolis-coverage`).
