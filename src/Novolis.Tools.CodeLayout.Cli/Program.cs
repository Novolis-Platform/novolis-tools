using System.Text.Json;
using System.CommandLine;
using Novolis.Tools.CodeLayout;

var root = new RootCommand("""
    novolis-code-layout — detect and repair C# file layout across an SLNX solution.

    detect reports files with multiple top-level types and type-less source files.
    detect-and-fix moves extra types into their own files. Add --delete to remove only
    explicitly classified empty-file deletion candidates.
    """);

var detect = new Command("detect", "Detect C# file-layout issues without changing files.");
detect.Aliases.Add("map");
var detectOptions = AddScanOptions(detect);
detect.SetAction(async (parseResult, cancellationToken) =>
    await RunAsync(
        parseResult,
        async () =>
        {
            var report = await ScanAsync(parseResult, detectOptions, cancellationToken)
                .ConfigureAwait(false);
            if (parseResult.GetValue(detectOptions.Json))
                WriteJson(report);
            else
                WriteHumanReport(report);
            return HasErrors(report.Diagnostics) ? 1 : 0;
        }).ConfigureAwait(false));
root.Subcommands.Add(detect);

var detectAndFix = new Command("detect-and-fix", "Detect and fix C# file-layout issues.");
detectAndFix.Aliases.Add("fix");
var fixOptions = AddScanOptions(detectAndFix);
var deleteOption = new Option<bool>("--delete")
{
    Description = "Delete only files reported as safe deletion candidates.",
    DefaultValueFactory = _ => false,
};
detectAndFix.Options.Add(deleteOption);
detectAndFix.SetAction(async (parseResult, cancellationToken) =>
    await RunAsync(
        parseResult,
        async () =>
        {
            var report = await ScanAsync(parseResult, fixOptions, cancellationToken)
                .ConfigureAwait(false);
            var result = await new CodeLayoutFixer().ApplyAsync(
                    report,
                    parseResult.GetValue(deleteOption),
                    cancellationToken)
                .ConfigureAwait(false);

            if (parseResult.GetValue(fixOptions.Json))
                WriteJson(result);
            else
                WriteHumanFixResult(result, parseResult.GetValue(deleteOption));

            return HasErrors(report.Diagnostics) || HasErrors(result.Diagnostics) ? 1 : 0;
        }).ConfigureAwait(false));
root.Subcommands.Add(detectAndFix);

if (args.Length > 0
    && !args[0].Equals("detect", StringComparison.OrdinalIgnoreCase)
    && !args[0].Equals("detect-and-fix", StringComparison.OrdinalIgnoreCase)
    && !args[0].Equals("map", StringComparison.OrdinalIgnoreCase)
    && !args[0].Equals("fix", StringComparison.OrdinalIgnoreCase)
    && !args[0].Equals("help", StringComparison.OrdinalIgnoreCase)
    && !args[0].Equals("--help", StringComparison.OrdinalIgnoreCase)
    && !args[0].Equals("-h", StringComparison.OrdinalIgnoreCase))
{
    var directCommand = args.Any(argument =>
        string.Equals(argument, "--delete", StringComparison.OrdinalIgnoreCase))
        ? "detect-and-fix"
        : "detect";
    args = new[] { directCommand }.Concat(args).ToArray();
}

return await root.Parse(args).InvokeAsync().ConfigureAwait(false);

static ScanOptions AddScanOptions(Command command)
{
    var options = new ScanOptions
    {
        Solution = new Argument<string>("solution")
        {
            Description = "Path to the .slnx solution to scan.",
        },
        Json = new Option<bool>("--json")
        {
            Description = "Write a structured JSON map instead of human-readable output.",
            DefaultValueFactory = _ => false,
        },
        IncludeGenerated = new Option<bool>("--include-generated")
        {
            Description = "Include generated source files in the map. Generated files are never rewritten or deleted.",
            DefaultValueFactory = _ => false,
        },
        Configuration = new Option<string>("--configuration")
        {
            Description = "MSBuild configuration used for project evaluation.",
            DefaultValueFactory = _ => "Debug",
        },
        Platform = new Option<string>("--platform")
        {
            Description = "MSBuild platform used for project evaluation.",
            DefaultValueFactory = _ => "AnyCPU",
        },
        Framework = new Option<string?>("--framework")
        {
            Description = "Optional target framework used for project evaluation.",
        },
    };
    command.Arguments.Add(options.Solution);
    command.Options.Add(options.Json);
    command.Options.Add(options.IncludeGenerated);
    command.Options.Add(options.Configuration);
    command.Options.Add(options.Platform);
    command.Options.Add(options.Framework);
    return options;
}

static async ValueTask<CodeLayoutReport> ScanAsync(
    ParseResult parseResult,
    ScanOptions options,
    CancellationToken cancellationToken)
{
    var solution = parseResult.GetValue(options.Solution)
                   ?? throw new InvalidOperationException("A solution path is required.");
    return await new CodeLayoutScanner().ScanAsync(
            solution,
            new CodeLayoutScanOptions(
                parseResult.GetValue(options.Configuration) ?? "Debug",
                parseResult.GetValue(options.Platform) ?? "AnyCPU",
                parseResult.GetValue(options.Framework),
                parseResult.GetValue(options.IncludeGenerated)),
            cancellationToken)
        .ConfigureAwait(false);
}

static async ValueTask<int> RunAsync(
    ParseResult parseResult,
    Func<ValueTask<int>> action)
{
    try
    {
        return await action().ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        Console.Error.WriteLine("Operation cancelled.");
        return 2;
    }
    catch (Exception exception)
    {
        Console.Error.WriteLine($"error: {exception.Message}");
        return 1;
    }
}

static void WriteHumanReport(CodeLayoutReport report)
{
    Console.WriteLine($"solution  {report.SolutionPath}");
    Console.WriteLine($"projects  {report.Projects.Count}");
    Console.WriteLine($"files     {report.SourceFileCount}");
    Console.WriteLine($"multi     {report.MultiTypeFileCount}");
    Console.WriteLine($"type-less {report.TypeLessFileCount}");
    Console.WriteLine($"delete    {report.DeletionCandidateCount}");

    foreach (var project in report.Projects)
    {
        foreach (var file in project.Files.Where(file => file.ExtraTypes.Count > 0))
        {
            Console.WriteLine($"split     {file.RelativePath}");
            Console.WriteLine($"          keep {file.PrimaryType ?? "first type"}");
            foreach (var type in file.ExtraTypes)
                Console.WriteLine($"          move {type}");
        }

        foreach (var file in project.Files.Where(file => file.TypeLess))
        {
            var marker = file.DeletionCandidate ? "delete-candidate" : "keep";
            Console.WriteLine($"type-less {marker} {file.RelativePath} ({file.TypeLessKind})");
        }

        if (project.GeneratedFilesSkipped > 0)
            Console.WriteLine($"generated {project.GeneratedFilesSkipped} skipped in {project.ProjectName}");
    }

    WriteDiagnostics(report.Diagnostics);
}

static void WriteHumanFixResult(CodeLayoutFixResult result, bool deleteRequested)
{
    Console.WriteLine($"solution  {result.Before.SolutionPath}");
    Console.WriteLine($"split     {result.Changes.Count(change => change.Detail.Contains("Moved ", StringComparison.Ordinal))}");
    Console.WriteLine($"written   {result.FilesWritten}");
    Console.WriteLine($"deleted   {result.FilesDeleted}");
    if (!deleteRequested && result.Before.DeletionCandidateCount > 0)
        Console.WriteLine($"skipped   {result.Before.DeletionCandidateCount} deletion candidate(s); pass --delete to remove them");

    foreach (var change in result.Changes)
        Console.WriteLine($"{change.Kind,-15} {change.FilePath} — {change.Detail}");
    WriteDiagnostics(result.Before.Diagnostics);
    WriteDiagnostics(result.Diagnostics);
}

static void WriteDiagnostics(IEnumerable<CodeLayoutDiagnostic> diagnostics)
{
    foreach (var diagnostic in diagnostics)
        Console.Error.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message} {diagnostic.Path}");
}

static bool HasErrors(IEnumerable<CodeLayoutDiagnostic> diagnostics) =>
    diagnostics.Any(diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.OrdinalIgnoreCase));

static void WriteJson<T>(T value) =>
    Console.WriteLine(JsonSerializer.Serialize(
        value,
        new JsonSerializerOptions { WriteIndented = true }));

sealed class ScanOptions
{
    public required Argument<string> Solution { get; init; }
    public required Option<bool> Json { get; init; }
    public required Option<bool> IncludeGenerated { get; init; }
    public required Option<string> Configuration { get; init; }
    public required Option<string> Platform { get; init; }
    public required Option<string?> Framework { get; init; }
}
