# Novolis.Tools.Coverage

Library for org / Platform.slnx Cobertura collection, package gap analysis, CRAP (Change Risk Anti-Patterns) scoring, and line+branch gates.

**CLIs are thin:** use `Novolis.Tools.Coverage.Cli` (`novolis-coverage`) — it only binds options and calls this library.

## Install

```powershell
dotnet add package Novolis.Tools.Coverage --version 2026.1.*
dotnet tool install --global Novolis.Tools.Coverage.Cli --version 2026.1.*
```

## Collect (library)

```csharp
using Novolis.Tools.Coverage;

var root = CoverageWorkspace.ResolveRoot();
var result = await new CoverageCollector(Console.Out).CollectAsync(new CoverageCollectOptions
{
    Root = root,
    OutputDir = Path.Combine(root, "coverage"),
    PlatformSlnx = true,
    SkipBuild = true,
    FailBelow = 95, // line OR branch
    FlattenHtml = true,
});
```

## Analyze gaps (no test run)

```csharp
var doc = CoberturaDocumentParser.Load(@"d:\novolis\coverage\report\Cobertura.xml");
var shortfall = CoverageAnalyzer.Shortfall(doc.Summary, targetPercent: 95);
var top = CoverageAnalyzer.TopBranchGaps(doc, take: 25);
Console.Write(CoverageAnalyzer.FormatGapsMarkdown(doc));
CoverageAssert.AtLeast(doc.Summary, failBelow: 95);
```

## CRAP (risk hotspots)

Uses Coverlet Cobertura method rows (`complexity` + `line-rate`):

`CRAP(m) = CC² × (1 − lineCoverage)³ + CC`

```csharp
var report = CrapAnalyzer.Analyze(doc, threshold: 30);
var md = CrapAnalyzer.FormatMarkdown(report, flaggedOnly: true);
CrapAnalyzer.WriteReport(md); // ./CRAP.md under cwd, or pass --out path
```

## CLI (orchestrator)

```powershell
novolis-coverage collect --platform --fail-below 95 --out d:\novolis\coverage
novolis-coverage list --platform
novolis-coverage gaps --cobertura d:\novolis\coverage\report\Cobertura.xml --target 95 --write d:\novolis\coverage\GAPS.md
# One markdown file under the caller's cwd (or --out)
novolis-coverage crap --cobertura d:\novolis\coverage\Cobertura.xml --flagged-only --fail-above -1
novolis-coverage crap --out d:\novolis\CRAP.md
```

## Test authoring

For public-API smoke helpers while closing gaps, see `Novolis.Testing.Coverage`.
