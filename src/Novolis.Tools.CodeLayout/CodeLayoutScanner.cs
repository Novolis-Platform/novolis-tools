using Microsoft.CodeAnalysis.Text;
using Novolis.Workspaces.DotNet;
using Novolis.Workspaces.DotNet.MSBuild;
using Novolis.Workspaces.DotNet.Slnx;

namespace Novolis.Tools.CodeLayout;

/// <summary>Maps C# file layout across every project listed by an SLNX solution.</summary>
public sealed class CodeLayoutScanner
{
    private readonly SlnxSolutionReader _solutionReader;
    private readonly MsBuildProjectEvaluator _projectEvaluator;

    /// <summary>Creates a scanner using the platform SLNX and MSBuild readers.</summary>
    public CodeLayoutScanner(
        SlnxSolutionReader? solutionReader = null,
        MsBuildProjectEvaluator? projectEvaluator = null)
    {
        _solutionReader = solutionReader ?? new SlnxSolutionReader();
        _projectEvaluator = projectEvaluator ?? new MsBuildProjectEvaluator();
    }

    /// <summary>Scan all C# compile items in the supplied SLNX file.</summary>
    public async ValueTask<CodeLayoutReport> ScanAsync(
        string solutionPath,
        CodeLayoutScanOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new CodeLayoutScanOptions();
        var fullSolutionPath = Path.GetFullPath(solutionPath);
        if (!fullSolutionPath.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The layout tool accepts an .slnx solution path.", nameof(solutionPath));
        if (!File.Exists(fullSolutionPath))
            throw new FileNotFoundException("Solution file does not exist.", fullSolutionPath);

        var solutionRoot = Path.GetDirectoryName(fullSolutionPath)
                           ?? throw new InvalidOperationException(fullSolutionPath);
        var fileSystem = new System.IO.Abstractions.FileSystem();
        var workspace = new SolutionWorkspace(
            fileSystem.DirectoryInfo.New(solutionRoot),
            new SolutionFilePath(fullSolutionPath));
        var topology = await _solutionReader.ReadAsync(workspace, cancellationToken)
            .ConfigureAwait(false);
        var diagnostics = topology.Diagnostics
            .Select(ToDiagnostic)
            .ToList();
        var projects = new List<PendingProject>();

        foreach (var projectEntry in topology.Projects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            projects.Add(await ReadProjectAsync(
                    projectEntry,
                    solutionRoot,
                    options,
                    cancellationToken)
                .ConfigureAwait(false));
        }

        var references = projects
            .SelectMany(project => project.Files)
            .GroupBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.OrdinalIgnoreCase);

        var resultProjects = projects
            .Select(project => ToProjectReport(project, references))
            .ToArray();
        diagnostics.AddRange(projects.SelectMany(project => project.Diagnostics));

        return new CodeLayoutReport(fullSolutionPath, resultProjects, diagnostics);
    }

    private async ValueTask<PendingProject> ReadProjectAsync(
        SlnxProjectEntry projectEntry,
        string solutionRoot,
        CodeLayoutScanOptions options,
        CancellationToken cancellationToken)
    {
        var projectDiagnostics = new List<CodeLayoutDiagnostic>();
        if (!File.Exists(projectEntry.FullPath))
        {
            projectDiagnostics.Add(new CodeLayoutDiagnostic(
                "NCL1001",
                "Project file does not exist.",
                "error",
                projectEntry.FullPath));
            return new PendingProject(
                projectEntry,
                true,
                false,
                0,
                [],
                projectDiagnostics);
        }

        var context = new EvaluationContext(
            options.Configuration,
            options.Platform,
            options.TargetFramework,
            AllowEvaluation: true);
        var evaluation = await _projectEvaluator.EvaluateAsync(
                projectEntry.FullPath,
                context,
                cancellationToken)
            .ConfigureAwait(false);
        projectDiagnostics.AddRange(evaluation.Diagnostics.Select(ToDiagnostic));

        var usesDefaultCompileItems = UsesDefaultCompileItems(evaluation.Properties);
        var candidates = evaluation.Items
            .Where(item => string.Equals(item.ItemType, "Compile", StringComparison.OrdinalIgnoreCase))
            .Select(item => new SourceCandidate(
                ResolveItemPath(projectEntry.FullPath, item.EvaluatedInclude),
                IsGenerated(item.EvaluatedInclude, item.Metadata)))
            .Where(candidate => File.Exists(candidate.FilePath))
            .GroupBy(candidate => candidate.FilePath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
        var usedDirectoryFallback = false;

        if (candidates.Count == 0 && options.AllowDirectoryFallback)
        {
            usedDirectoryFallback = true;
            var projectDirectory = Path.GetDirectoryName(projectEntry.FullPath)!;
            candidates = Directory.EnumerateFiles(projectDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !IsBuildOutput(path))
                .Select(path => new SourceCandidate(Path.GetFullPath(path), IsGenerated(path, null)))
                .ToList();
            projectDiagnostics.Add(new CodeLayoutDiagnostic(
                "NCL1002",
                "The project evaluated without Compile items; the project directory was scanned as a fallback.",
                "warning",
                projectEntry.FullPath));
        }

        var generatedFilesSkipped = 0;
        var files = new List<PendingFile>();
        foreach (var candidate in candidates.OrderBy(item => item.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (candidate.Generated && !options.IncludeGenerated)
            {
                generatedFilesSkipped++;
                continue;
            }

            try
            {
                var source = await ReadSourceTextAsync(candidate.FilePath, cancellationToken)
                    .ConfigureAwait(false);
                var analysis = CodeLayoutSyntax.Analyze(candidate.FilePath, source);
                var primary = CodeLayoutSyntax.SelectPrimaryType(candidate.FilePath, analysis.Types);
                var typeReports = analysis.Types
                    .Select(type => new CodeLayoutType(
                        type.FullName,
                        type.Name,
                        type.Namespace,
                        type.Kind,
                        type.Declarations.Count))
                    .ToArray();
                var extra = analysis.Types
                    .Where(type => primary is not null && !string.Equals(type.Key, primary, StringComparison.Ordinal))
                    .Select(type => type.FullName)
                    .ToArray();
                var relativePath = Path.GetRelativePath(solutionRoot, candidate.FilePath);
                var typeLess = analysis.Types.Count == 0;

                files.Add(new PendingFile(
                    candidate.FilePath,
                    relativePath,
                    candidate.Generated,
                    typeReports,
                    primary is null
                        ? null
                        : typeReports.First(type =>
                            string.Equals(type.FullName, analysis.Types.First(item => item.Key == primary).FullName, StringComparison.Ordinal))
                            .FullName,
                    extra,
                    typeLess,
                    typeLess ? analysis.TypeLessKind : "none",
                    analysis.DeletionCandidate && !candidate.Generated,
                    analysis.DeletionReason,
                    analysis));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                projectDiagnostics.Add(new CodeLayoutDiagnostic(
                    "NCL1003",
                    exception.Message,
                    "error",
                    candidate.FilePath));
            }
        }

        return new PendingProject(
            projectEntry,
            usesDefaultCompileItems,
            usedDirectoryFallback,
            generatedFilesSkipped,
            files,
            projectDiagnostics);
    }

    private static CodeLayoutProject ToProjectReport(
        PendingProject project,
        IReadOnlyDictionary<string, int> references)
    {
        var files = project.Files
            .Select(file =>
            {
                var shared = references.TryGetValue(file.FilePath, out var count) && count > 1;
                return new CodeLayoutFile(
                    project.ProjectEntry.FullPath,
                    file.FilePath,
                    file.RelativePath,
                    file.Generated,
                    shared,
                    file.Types,
                    file.PrimaryType,
                    file.ExtraTypes,
                    file.TypeLess,
                    file.TypeLessKind,
                    file.DeletionCandidate && !shared,
                    file.DeletionCandidate && !shared ? file.DeletionReason : shared
                        ? "The file is compiled by more than one project."
                        : file.DeletionReason);
            })
            .ToArray();

        return new CodeLayoutProject(
            project.ProjectEntry.FullPath,
            project.ProjectEntry.Name
                ?? Path.GetFileNameWithoutExtension(project.ProjectEntry.FullPath),
            project.UsesDefaultCompileItems,
            project.UsedDirectoryFallback,
            project.GeneratedFilesSkipped,
            files,
            project.Diagnostics);
    }

    private static async ValueTask<SourceText> ReadSourceTextAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        cancellationToken.ThrowIfCancellationRequested();
        return SourceText.From(stream);
    }

    private static string ResolveItemPath(string projectPath, string include)
    {
        var normalized = include
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.IsPathRooted(normalized)
            ? normalized
            : Path.Combine(Path.GetDirectoryName(projectPath)!, normalized));
    }

    private static bool UsesDefaultCompileItems(IReadOnlyDictionary<string, string> properties)
    {
        if (properties.TryGetValue("EnableDefaultItems", out var defaultItems)
            && string.Equals(defaultItems, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !properties.TryGetValue("EnableDefaultCompileItems", out var compileItems)
               || !string.Equals(compileItems, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGenerated(string path, IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is not null)
        {
            foreach (var name in new[] { "AutoGen", "DesignTime", "Generated" })
            {
                if (metadata.TryGetValue(name, out var value)
                    && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        var normalized = path.Replace('\\', '/');
        var fileName = Path.GetFileName(path);
        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/Generated/", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
               || fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBuildOutput(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
               || normalized.Contains("/.git/", StringComparison.OrdinalIgnoreCase);
    }

    private static CodeLayoutDiagnostic ToDiagnostic(WorkspaceDiagnostic diagnostic) =>
        new(
            diagnostic.Code,
            diagnostic.Message,
            diagnostic.Severity.ToString().ToLowerInvariant(),
            diagnostic.Path);

    private sealed record SourceCandidate(string FilePath, bool Generated);

    private sealed record PendingFile(
        string FilePath,
        string RelativePath,
        bool Generated,
        IReadOnlyList<CodeLayoutType> Types,
        string? PrimaryType,
        IReadOnlyList<string> ExtraTypes,
        bool TypeLess,
        string TypeLessKind,
        bool DeletionCandidate,
        string? DeletionReason,
        CodeLayoutSyntaxAnalysis Analysis);

    private sealed record PendingProject(
        SlnxProjectEntry ProjectEntry,
        bool UsesDefaultCompileItems,
        bool UsedDirectoryFallback,
        int GeneratedFilesSkipped,
        IReadOnlyList<PendingFile> Files,
        IReadOnlyList<CodeLayoutDiagnostic> Diagnostics);
}
