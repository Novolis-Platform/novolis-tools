using System.CommandLine;
using Novolis.Tools.Coverage;

var root = new RootCommand(
    "novolis-coverage — thin CLI over Novolis.Tools.Coverage (collect / list / gaps / crap)");

var collectCommand = new Command("collect", "Run tests with coverage and merge ReportGenerator HTML");
var listCommand = new Command("list", "List repos / test hosts that would run (no collection)");
var gapsCommand = new Command("gaps", "Analyze an existing Cobertura.xml for package gaps (library-only; no test run)");
var crapCommand = new Command(
    "crap",
    "Platform.slnx CRAP report: parallel Cobertura fan-in → one markdown file (no per-repo reports)");

static CoverageCollectOptions BindCollectOptions(
    ParseResult parseResult,
    Option<string?> rootOpt,
    Option<string?> outOpt,
    Option<bool> platformOpt,
    Option<string?> platformPathOpt,
    Option<bool> regenOpt,
    Option<string> configOpt,
    Option<int> throttleOpt,
    Option<bool> skipBuildOpt,
    Option<double> failBelowOpt,
    Option<string[]> excludeOpt,
    Option<string[]> includeOpt,
    Option<string?> excludeFileOpt,
    Option<bool> flattenOpt,
    Option<bool> openOpt,
    bool listOnly)
{
    var rootPath = CoverageWorkspace.ResolveRoot(parseResult.GetValue(rootOpt));
    var output = parseResult.GetValue(outOpt);
    if (string.IsNullOrWhiteSpace(output))
        output = Path.Combine(rootPath, "coverage");

    return new CoverageCollectOptions
    {
        Root = rootPath,
        OutputDir = Path.GetFullPath(output),
        PlatformSlnx = parseResult.GetValue(platformOpt),
        PlatformSlnxPath = parseResult.GetValue(platformPathOpt),
        RegenerateSlnx = parseResult.GetValue(regenOpt),
        Configuration = parseResult.GetValue(configOpt) ?? "Debug",
        ThrottleLimit = parseResult.GetValue(throttleOpt),
        SkipBuild = parseResult.GetValue(skipBuildOpt),
        FailBelow = parseResult.GetValue(failBelowOpt),
        Exclude = parseResult.GetValue(excludeOpt) ?? [],
        Include = parseResult.GetValue(includeOpt) ?? [],
        ExcludeFile = parseResult.GetValue(excludeFileOpt),
        FlattenHtml = parseResult.GetValue(flattenOpt),
        OpenReport = parseResult.GetValue(openOpt),
        ListOnly = listOnly,
    };
}

Option<string?> AddRoot(Command cmd)
{
    var o = new Option<string?>("--root") { Description = "Novolis workspace root (default: NOVOLIS_ROOT or walk from cwd)" };
    cmd.Options.Add(o);
    return o;
}

Option<string?> AddOut(Command cmd)
{
    var o = new Option<string?>("--out") { Description = "Output directory (default: <root>/coverage)" };
    cmd.Options.Add(o);
    return o;
}

Option<bool> AddPlatform(Command cmd)
{
    var o = new Option<bool>("--platform") { Description = "Use Novolis.Platform.slnx + ProjectReference mode", DefaultValueFactory = _ => false };
    cmd.Options.Add(o);
    return o;
}

Option<string?> AddPlatformPath(Command cmd)
{
    var o = new Option<string?>("--platform-slnx") { Description = "Explicit path to Novolis.Platform.slnx" };
    cmd.Options.Add(o);
    return o;
}

Option<bool> AddRegen(Command cmd)
{
    var o = new Option<bool>("--regenerate-slnx") { Description = "Regenerate Platform.slnx before collect", DefaultValueFactory = _ => false };
    cmd.Options.Add(o);
    return o;
}

Option<string> AddConfig(Command cmd)
{
    var o = new Option<string>("--configuration")
    {
        Description = "Build/test configuration",
        DefaultValueFactory = _ => "Debug",
    };
    cmd.Options.Add(o);
    return o;
}

Option<int> AddThrottle(Command cmd)
{
    var o = new Option<int>("--throttle")
    {
        Description = "Max parallel repos (default: ProcessorCount - 1)",
        DefaultValueFactory = _ => 0,
    };
    cmd.Options.Add(o);
    return o;
}

Option<bool> AddSkipBuild(Command cmd)
{
    var o = new Option<bool>("--skip-build") { Description = "Pass --no-build to dotnet test", DefaultValueFactory = _ => false };
    cmd.Options.Add(o);
    return o;
}

Option<double> AddFailBelow(Command cmd)
{
    var o = new Option<double>("--fail-below")
    {
        Description = "Fail if aggregate line OR branch %% is below this (Platform default 95 when 0; use -1 to disable)",
        DefaultValueFactory = _ => 0,
    };
    cmd.Options.Add(o);
    return o;
}

Option<string[]> AddExclude(Command cmd)
{
    var o = new Option<string[]>("--exclude")
    {
        Description = "Extra repo names to exclude (repeatable or comma-separated)",
        AllowMultipleArgumentsPerToken = true,
        DefaultValueFactory = _ => [],
    };
    cmd.Options.Add(o);
    return o;
}

Option<string[]> AddInclude(Command cmd)
{
    var o = new Option<string[]>("--include")
    {
        Description = "Only these repos (still applies excludes)",
        AllowMultipleArgumentsPerToken = true,
        DefaultValueFactory = _ => [],
    };
    cmd.Options.Add(o);
    return o;
}

Option<string?> AddExcludeFile(Command cmd)
{
    var o = new Option<string?>("--exclude-file") { Description = "Exclude list file (default: governance coverage-excludes.txt)" };
    cmd.Options.Add(o);
    return o;
}

Option<bool> AddFlatten(Command cmd)
{
    var o = new Option<bool>("--flatten") { Description = "Place index.html at --out root", DefaultValueFactory = _ => true };
    cmd.Options.Add(o);
    return o;
}

Option<bool> AddOpen(Command cmd)
{
    var o = new Option<bool>("--open") { Description = "Open HTML report when done (Windows)", DefaultValueFactory = _ => false };
    cmd.Options.Add(o);
    return o;
}

void WireCollectLike(Command cmd, bool listOnly)
{
    var cRoot = AddRoot(cmd);
    var cOut = AddOut(cmd);
    var cPlatform = AddPlatform(cmd);
    var cPlatformPath = AddPlatformPath(cmd);
    var cRegen = AddRegen(cmd);
    var cConfig = AddConfig(cmd);
    var cThrottle = AddThrottle(cmd);
    var cSkip = AddSkipBuild(cmd);
    var cFail = AddFailBelow(cmd);
    var cExclude = AddExclude(cmd);
    var cInclude = AddInclude(cmd);
    var cExcludeFile = AddExcludeFile(cmd);
    var cFlatten = AddFlatten(cmd);
    var cOpen = AddOpen(cmd);

    cmd.SetAction(async parseResult =>
    {
        var options = BindCollectOptions(
            parseResult, cRoot, cOut, cPlatform, cPlatformPath, cRegen, cConfig, cThrottle, cSkip, cFail,
            cExclude, cInclude, cExcludeFile, cFlatten, cOpen, listOnly);
        var collector = new CoverageCollector(Console.Out);
        var result = await collector.CollectAsync(options);
        if (listOnly)
            return 0;
        if (result.Repos.Any(r => r.Status == "fail") || result.GateFailed)
            return 1;
        return 0;
    });
}

WireCollectLike(collectCommand, listOnly: false);
WireCollectLike(listCommand, listOnly: true);

var gapsCobertura = new Option<string?>("--cobertura")
{
    Description = "Path to Cobertura.xml (default: <root>/coverage/report/Cobertura.xml or <root>/coverage/Cobertura.xml)",
};
gapsCommand.Options.Add(gapsCobertura);
var gapsRoot = AddRoot(gapsCommand);
var gapsTarget = new Option<double>("--target")
{
    Description = "Target line/branch percent",
    DefaultValueFactory = _ => 95,
};
gapsCommand.Options.Add(gapsTarget);
var gapsTake = new Option<int>("--take")
{
    Description = "Max packages to list",
    DefaultValueFactory = _ => 30,
};
gapsCommand.Options.Add(gapsTake);
var gapsOut = new Option<string?>("--write")
{
    Description = "Optional path to write GAPS.md",
};
gapsCommand.Options.Add(gapsOut);
var gapsFailBelow = new Option<double>("--fail-below")
{
    Description = "Exit 1 if aggregate is below this (default: same as --target; use -1 to disable)",
    DefaultValueFactory = _ => 0,
};
gapsCommand.Options.Add(gapsFailBelow);

gapsCommand.SetAction(parseResult =>
{
    var workspace = CoverageWorkspace.ResolveRoot(parseResult.GetValue(gapsRoot));
    var cobertura = ResolveCoberturaPath(workspace, parseResult.GetValue(gapsCobertura));
    var target = parseResult.GetValue(gapsTarget);
    var take = parseResult.GetValue(gapsTake);
    var failBelow = parseResult.GetValue(gapsFailBelow);
    if (failBelow == 0)
        failBelow = target;

    var document = CoberturaDocumentParser.Load(cobertura);
    var md = CoverageAnalyzer.FormatGapsMarkdown(document, target, take);
    Console.WriteLine(md);

    var writePath = parseResult.GetValue(gapsOut);
    if (!string.IsNullOrWhiteSpace(writePath))
    {
        var full = Path.GetFullPath(writePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, md);
        Console.WriteLine($"Wrote {full}");
    }

    var (failed, message) = CoverageGate.Evaluate(document.Summary, failBelow);
    if (failed)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    return 0;
});

static string ResolveCoberturaPath(string workspace, string? explicitPath)
{
    if (!string.IsNullOrWhiteSpace(explicitPath))
        return Path.GetFullPath(explicitPath);

    var candidates = new[]
    {
        Path.Combine(workspace, "coverage", "Cobertura.xml"),
        Path.Combine(workspace, "coverage", "report", "Cobertura.xml"),
        Path.Combine(workspace, "artifacts", "coverage", "Cobertura.xml"),
        Path.Combine(workspace, "artifacts", "coverage", "report", "Cobertura.xml"),
    };
    return candidates.FirstOrDefault(File.Exists)
           ?? throw new FileNotFoundException(
               "Cobertura.xml not found. Pass --cobertura or run collect first.");
}

var crapCobertura = new Option<string?>("--cobertura")
{
    Description = "Optional single Cobertura.xml (skips Platform.slnx multi-file discovery)",
};
crapCommand.Options.Add(crapCobertura);
var crapRoot = AddRoot(crapCommand);
var crapPlatformPath = AddPlatformPath(crapCommand);
var crapCoverageDir = new Option<string?>("--coverage-dir")
{
    Description = "Coverage output root from collect (default: <root>/coverage)",
};
crapCommand.Options.Add(crapCoverageDir);
var crapOut = new Option<string?>("--out")
{
    Description = "Single report file (default: ./CRAP.md under the caller's cwd). Directory → CRAP.md inside it.",
};
crapCommand.Options.Add(crapOut);
var crapThreshold = new Option<double>("--threshold")
{
    Description = "Flag methods with CRAP above this value",
    DefaultValueFactory = _ => CrapScore.DefaultThreshold,
};
crapCommand.Options.Add(crapThreshold);
var crapTake = new Option<int>("--take")
{
    Description = "Max methods in the report table",
    DefaultValueFactory = _ => 200,
};
crapCommand.Options.Add(crapTake);
var crapFlaggedOnly = new Option<bool>("--all")
{
    Description = "Include non-flagged methods in the table (default: flagged only)",
    DefaultValueFactory = _ => false,
};
crapCommand.Options.Add(crapFlaggedOnly);
var crapFailAbove = new Option<double>("--fail-above")
{
    Description = "Exit 1 if any method CRAP exceeds this (default: same as --threshold; use -1 to disable)",
    DefaultValueFactory = _ => 0,
};
crapCommand.Options.Add(crapFailAbove);
var crapThrottle = AddThrottle(crapCommand);
var crapExclude = AddExclude(crapCommand);
var crapInclude = AddInclude(crapCommand);
var crapExcludeFile = AddExcludeFile(crapCommand);

crapCommand.SetAction(parseResult =>
{
    var workspace = CoverageWorkspace.ResolveRoot(parseResult.GetValue(crapRoot));
    var threshold = parseResult.GetValue(crapThreshold);
    var take = parseResult.GetValue(crapTake);
    var flaggedOnly = !parseResult.GetValue(crapFlaggedOnly);
    var failAbove = parseResult.GetValue(crapFailAbove);
    if (failAbove == 0)
        failAbove = threshold;

    var explicitCobertura = parseResult.GetValue(crapCobertura);
    var options = new CrapAnalyzeOptions
    {
        Root = workspace,
        PlatformSlnxPath = parseResult.GetValue(crapPlatformPath),
        CoverageDir = parseResult.GetValue(crapCoverageDir),
        CoberturaPaths = string.IsNullOrWhiteSpace(explicitCobertura)
            ? []
            : [explicitCobertura],
        Threshold = threshold,
        MaxDegreeOfParallelism = parseResult.GetValue(crapThrottle),
        Exclude = parseResult.GetValue(crapExclude) ?? [],
        Include = parseResult.GetValue(crapInclude) ?? [],
        ExcludeFile = parseResult.GetValue(crapExcludeFile),
    };

    var report = CrapAnalyzer.AnalyzePlatform(options);
    var md = CrapAnalyzer.FormatMarkdown(report, tableTake: take, flaggedOnly: flaggedOnly);
    var written = CrapAnalyzer.WriteReport(md, parseResult.GetValue(crapOut));

    var inv = System.Globalization.CultureInfo.InvariantCulture;
    Console.WriteLine(
        $"CRAP (Platform.slnx): {report.SourcePaths.Count.ToString(inv)} Cobertura file(s), " +
        $"dop={report.DegreeOfParallelism.ToString(inv)}, scored {report.Methods.Count.ToString(inv)}, " +
        $"flagged {report.FlaggedCount.ToString(inv)}, max {report.MaxScore.ToString("0.##", inv)} " +
        $"(threshold {threshold.ToString("0.#", inv)})");
    if (!string.IsNullOrWhiteSpace(report.PlatformSlnxPath))
        Console.WriteLine($"Platform: {report.PlatformSlnxPath}");
    Console.WriteLine($"Wrote {written}");

    var (failed, message) = CrapAnalyzer.EvaluateGate(report, failAbove);
    if (failed)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    return 0;
});

root.Subcommands.Add(collectCommand);
root.Subcommands.Add(listCommand);
root.Subcommands.Add(gapsCommand);
root.Subcommands.Add(crapCommand);

return root.Parse(args).Invoke();
