using System.CommandLine;
using Novolis.Tools.Coverage;

var root = new RootCommand("novolis-coverage — collect MTP Cobertura and merge HTML reports for Novolis workspaces");

var collectCommand = new Command("collect", "Run tests with coverage and merge ReportGenerator HTML");
var listCommand = new Command("list", "List repos / test hosts that would run (no collection)");

static CoverageCollectOptions BindOptions(
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
        Description = "Fail if aggregate line %% is below this (Platform default 95 when 0; use -1 to disable)",
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

var cRoot = AddRoot(collectCommand);
var cOut = AddOut(collectCommand);
var cPlatform = AddPlatform(collectCommand);
var cPlatformPath = AddPlatformPath(collectCommand);
var cRegen = AddRegen(collectCommand);
var cConfig = AddConfig(collectCommand);
var cThrottle = AddThrottle(collectCommand);
var cSkip = AddSkipBuild(collectCommand);
var cFail = AddFailBelow(collectCommand);
var cExclude = AddExclude(collectCommand);
var cInclude = AddInclude(collectCommand);
var cExcludeFile = AddExcludeFile(collectCommand);
var cFlatten = AddFlatten(collectCommand);
var cOpen = AddOpen(collectCommand);

collectCommand.SetAction(async parseResult =>
{
    var options = BindOptions(
        parseResult, cRoot, cOut, cPlatform, cPlatformPath, cRegen, cConfig, cThrottle, cSkip, cFail,
        cExclude, cInclude, cExcludeFile, cFlatten, cOpen, listOnly: false);
    var collector = new CoverageCollector(Console.Out);
    var result = await collector.CollectAsync(options);
    if (result.Repos.Any(r => r.Status == "fail") || result.GateFailed)
        return 1;
    return 0;
});

var lRoot = AddRoot(listCommand);
var lOut = AddOut(listCommand);
var lPlatform = AddPlatform(listCommand);
var lPlatformPath = AddPlatformPath(listCommand);
var lRegen = AddRegen(listCommand);
var lConfig = AddConfig(listCommand);
var lThrottle = AddThrottle(listCommand);
var lSkip = AddSkipBuild(listCommand);
var lFail = AddFailBelow(listCommand);
var lExclude = AddExclude(listCommand);
var lInclude = AddInclude(listCommand);
var lExcludeFile = AddExcludeFile(listCommand);
var lFlatten = AddFlatten(listCommand);
var lOpen = AddOpen(listCommand);

listCommand.SetAction(async parseResult =>
{
    var options = BindOptions(
        parseResult, lRoot, lOut, lPlatform, lPlatformPath, lRegen, lConfig, lThrottle, lSkip, lFail,
        lExclude, lInclude, lExcludeFile, lFlatten, lOpen, listOnly: true);
    var collector = new CoverageCollector(Console.Out);
    _ = await collector.CollectAsync(options);
    return 0;
});

root.Subcommands.Add(collectCommand);
root.Subcommands.Add(listCommand);

return root.Parse(args).Invoke();
