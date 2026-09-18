using System.Text.Json;
using Novolis.Workspaces.DotNet;
using Novolis.Workspaces.DotNet.Indexing;
using Novolis.Workspaces.DotNet.MSBuild;
using Novolis.Workspaces.DotNet.Slnx;

namespace Novolis.Solution.Tool;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static async Task<int> Main(string[] args)
    {
        if (args.Length is 0 || args[0] is "help" or "--help" or "-h")
            return PrintHelp();

        if (args.Length < 2)
            return Fail("A solution file path is required.");

        var command = args[0].ToLowerInvariant();
        var solutionPath = Path.GetFullPath(args[1]);
        if (!File.Exists(solutionPath))
            return Fail($"Solution file does not exist: {solutionPath}");

        var rootPath = Path.GetDirectoryName(solutionPath)!;
        var fileSystem = new System.IO.Abstractions.FileSystem();
        var workspace = new SolutionWorkspace(
            fileSystem.DirectoryInfo.New(rootPath),
            new SolutionFilePath(solutionPath));
        var asJson = HasFlag(args, "--json");

        return command switch
        {
            "topology" => await PrintTopologyAsync(workspace, asJson).ConfigureAwait(false),
            "catalog" => await PrintCatalogAsync(workspace, ParseContext(args), asJson).ConfigureAwait(false),
            _ => Fail($"Unknown command '{command}'. Use topology, catalog, or help."),
        };
    }

    private static async Task<int> PrintTopologyAsync(SolutionWorkspace workspace, bool asJson)
    {
        var topology = await new SlnxSolutionReader().ReadAsync(workspace).ConfigureAwait(false);
        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                solutionPath = workspace.SolutionFile.FullPath,
                topology.Projects,
                topology.Folders,
                topology.Diagnostics,
            }, JsonOptions));
            return HasErrors(topology.Diagnostics) ? 1 : 0;
        }

        Console.WriteLine(workspace.SolutionFile.FullPath);
        foreach (var project in topology.Projects)
            Console.WriteLine($"project  {project.RelativePath}");
        foreach (var folder in topology.Folders)
            Console.WriteLine($"folder   {folder.Name}");
        foreach (var diagnostic in topology.Diagnostics)
            Console.Error.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
        return HasErrors(topology.Diagnostics) ? 1 : 0;
    }

    private static async Task<int> PrintCatalogAsync(
        SolutionWorkspace workspace,
        EvaluationContext context,
        bool asJson)
    {
        MsBuildHostRegistration.EnsureRegistered();
        var catalog = await new SolutionCatalogBuilder().BuildAsync(workspace, context).ConfigureAwait(false);
        if (asJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(catalog, JsonOptions));
            return HasErrors(catalog.Diagnostics) ? 1 : 0;
        }

        Console.WriteLine($"snapshot {catalog.SnapshotId}");
        foreach (var project in catalog.Projects)
            Console.WriteLine($"project  {project.Project.RelativePath} ({project.Semantic.Types.Count} types)");
        foreach (var diagnostic in catalog.Diagnostics)
            Console.Error.WriteLine($"{diagnostic.Severity} {diagnostic.Code}: {diagnostic.Message}");
        return HasErrors(catalog.Diagnostics) ? 1 : 0;
    }

    private static EvaluationContext ParseContext(string[] args) =>
        new(
            GetOption(args, "--configuration") ?? "Debug",
            GetOption(args, "--platform") ?? "AnyCPU",
            GetOption(args, "--framework"),
            HasFlag(args, "--allow-evaluation"),
            HasFlag(args, "--allow-design-time-builds"));

    private static string? GetOption(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.OrdinalIgnoreCase))
                return args[index + 1];
        }

        return null;
    }

    private static bool HasFlag(IEnumerable<string> args, string name) =>
        args.Any(argument => string.Equals(argument, name, StringComparison.OrdinalIgnoreCase));

    private static bool HasErrors(IEnumerable<WorkspaceDiagnostic> diagnostics) =>
        diagnostics.Any(diagnostic => diagnostic.Severity is WorkspaceDiagnosticSeverity.Error);

    private static int PrintHelp()
    {
        Console.WriteLine("""
            novolis-solution
              topology <solution.slnx> [--json]
              catalog <solution.slnx> [--configuration Debug] [--platform AnyCPU]
                      [--framework net10.0] [--allow-evaluation]
                      [--allow-design-time-builds] [--json]
            """);
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
