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
// → <out>/index.html with Risk Hotspots (CRAP), CoverageHistory.html, badges; history in <out>/history
```

## Analyze gaps (no test run)

```csharp
var doc = CoberturaDocumentParser.Load(@"d:\novolis\coverage\report\Cobertura.xml");
var shortfall = CoverageAnalyzer.Shortfall(doc.Summary, targetPercent: 95);
var top = CoverageAnalyzer.TopBranchGaps(doc, take: 25);
Console.Write(CoverageAnalyzer.FormatGapsMarkdown(doc));
CoverageAssert.AtLeast(doc.Summary, failBelow: 95);
```

## CRAP (Platform.slnx risk hotspots)

Uses Coverlet Cobertura method rows (`complexity` + `line-rate`), scoped to
`Novolis.Platform.slnx`: discovers `coverage/report/novolis-*/Cobertura.xml`,
parses/scores in parallel, writes **one** merged markdown (no per-repo reports).

`CRAP(m) = CC² × (1 − lineCoverage)³ + CC`

```csharp
var report = CrapAnalyzer.AnalyzePlatform(new CrapAnalyzeOptions
{
    Root = CoverageWorkspace.ResolveRoot(),
    Threshold = 30,
});
var md = CrapAnalyzer.FormatMarkdown(report, flaggedOnly: true);
CrapAnalyzer.WriteReport(md); // ./CRAP.md under cwd
```

## CLI (orchestrator)

```powershell
novolis-coverage collect --platform --fail-below 95 --out d:\novolis\coverage
novolis-coverage list --platform
novolis-coverage gaps --cobertura d:\novolis\coverage\report\Cobertura.xml --target 95 --write d:\novolis\coverage\GAPS.md
# Platform.slnx parallel fan-in → one file (default ./CRAP.md)
novolis-coverage crap --fail-above -1
novolis-coverage crap --out d:\novolis\CRAP.md --throttle 8
```

## Test authoring

For public-API smoke helpers while closing gaps, see `Novolis.Testing.Coverage`.
