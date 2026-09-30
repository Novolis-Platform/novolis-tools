using System.Text;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.Text;

namespace Novolis.Tools.CodeLayout;

/// <summary>Applies the one-type-per-file layout and optional safe deletions.</summary>
public sealed class CodeLayoutFixer
{
    /// <summary>
    /// Split extra types in every mapped file. Type-less deletion candidates are only
    /// removed when <paramref name="delete"/> is true.
    /// </summary>
    public async ValueTask<CodeLayoutFixResult> ApplyAsync(
        CodeLayoutReport report,
        bool delete = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        var changes = new List<CodeLayoutChange>();
        var diagnostics = new List<CodeLayoutDiagnostic>();
        var occupiedPaths = new HashSet<string>(
            report.Files.Select(file => file.FilePath),
            StringComparer.OrdinalIgnoreCase);
        var projectReports = report.Projects
            .ToDictionary(project => project.ProjectPath, StringComparer.OrdinalIgnoreCase);

        foreach (var file in report.Files
                     .Where(file => file.ExtraTypes.Count > 0 && !file.Generated)
                     .GroupBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
                     .Select(group => group.First())
                     .OrderBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var source = await ReadSourceTextAsync(file.FilePath, cancellationToken)
                    .ConfigureAwait(false);
                var analysis = CodeLayoutSyntax.Analyze(file.FilePath, source);
                var primary = CodeLayoutSyntax.SelectPrimaryType(file.FilePath, analysis.Types);
                if (primary is null)
                    continue;

                var primaryKeys = new HashSet<string>(StringComparer.Ordinal) { primary };
                var updatedRoot = CodeLayoutSyntax.KeepTypes(analysis, primaryKeys, stripFileMetadata: false);
                var targetPaths = new List<(string Path, CodeLayoutTypeGroup Type)>();
                foreach (var type in analysis.Types.Where(type => !primaryKeys.Contains(type.Key)))
                {
                    var fileName = AllocateFileName(
                        occupiedPaths,
                        Path.GetDirectoryName(file.FilePath),
                        type.Name);
                    var targetPath = Path.Combine(Path.GetDirectoryName(file.FilePath)!, fileName);
                    var targetRoot = CodeLayoutSyntax.KeepTypes(
                        analysis,
                        new HashSet<string>(StringComparer.Ordinal) { type.Key },
                        stripFileMetadata: true);
                    var targetText = SourceText.From(
                        targetRoot.ToFullString(),
                        source.Encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                        source.ChecksumAlgorithm);

                    await WriteSourceTextAsync(targetPath, targetText, cancellationToken)
                        .ConfigureAwait(false);
                    occupiedPaths.Add(targetPath);
                    targetPaths.Add((targetPath, type));
                    changes.Add(new CodeLayoutChange(
                        "created",
                        targetPath,
                        $"Moved {type.FullName} from {file.FilePath}."));

                    foreach (var owner in OwnersOf(file.FilePath, report))
                    {
                        if (projectReports.TryGetValue(owner.ProjectPath, out var project)
                            && !project.UsesDefaultCompileItems)
                        {
                            if (ProjectFileEditor.EnsureCompileIncluded(owner.ProjectPath, targetPath))
                            {
                                changes.Add(new CodeLayoutChange(
                                    "project-updated",
                                    owner.ProjectPath,
                                    $"Added Compile Include for {targetPath}."));
                            }
                        }
                    }
                }

                var updatedText = SourceText.From(
                    updatedRoot.ToFullString(),
                    source.Encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    source.ChecksumAlgorithm);
                await WriteSourceTextAsync(file.FilePath, updatedText, cancellationToken)
                    .ConfigureAwait(false);
                changes.Add(new CodeLayoutChange(
                    "updated",
                    file.FilePath,
                    $"Kept {primary} and moved {targetPaths.Count} extra type(s)."));
            }
            catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or InvalidOperationException)
            {
                diagnostics.Add(new CodeLayoutDiagnostic(
                    "NCL2001",
                    exception.Message,
                    "error",
                    file.FilePath));
            }
        }

        if (delete)
        {
            foreach (var file in report.Files
                         .Where(file => file.DeletionCandidate && !file.Generated)
                         .GroupBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase)
                         .Select(group => group.First())
                         .OrderBy(file => file.FilePath, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (!File.Exists(file.FilePath))
                        continue;

                    File.Delete(file.FilePath);
                    changes.Add(new CodeLayoutChange(
                        "deleted",
                        file.FilePath,
                        file.DeletionReason ?? "Deleted type-less file."));

                    foreach (var owner in OwnersOf(file.FilePath, report))
                    {
                        if (projectReports.TryGetValue(owner.ProjectPath, out var project)
                            && !project.UsesDefaultCompileItems
                            && ProjectFileEditor.RemoveCompileIncluded(owner.ProjectPath, file.FilePath))
                        {
                            changes.Add(new CodeLayoutChange(
                                "project-updated",
                                owner.ProjectPath,
                                $"Removed Compile Include for {file.FilePath}."));
                        }
                    }
                }
                catch (Exception exception) when (exception is IOException
                                                  or UnauthorizedAccessException)
                {
                    diagnostics.Add(new CodeLayoutDiagnostic(
                        "NCL2002",
                        exception.Message,
                        "error",
                        file.FilePath));
                }
            }
        }

        return new CodeLayoutFixResult(report, changes, diagnostics);
    }

    private static IEnumerable<CodeLayoutFile> OwnersOf(
        string filePath,
        CodeLayoutReport report) =>
        report.Files
            .Where(file => string.Equals(file.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            .GroupBy(file => file.ProjectPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First());

    private static string AllocateFileName(
        IReadOnlySet<string> occupiedPaths,
        string? directory,
        string typeName)
    {
        if (directory is null)
            throw new InvalidOperationException("A source file directory is required for splitting.");

        var fileName = typeName + ".cs";
        if (!Conflicts(occupiedPaths, directory, fileName))
            return fileName;

        for (var index = 1; ; index++)
        {
            fileName = typeName + "." + index + ".cs";
            if (!Conflicts(occupiedPaths, directory, fileName))
                return fileName;
        }
    }

    private static bool Conflicts(
        IReadOnlySet<string> occupiedPaths,
        string directory,
        string fileName) =>
        occupiedPaths.Contains(Path.GetFullPath(Path.Combine(directory, fileName)));

    private static async ValueTask<SourceText> ReadSourceTextAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        cancellationToken.ThrowIfCancellationRequested();
        return SourceText.From(stream);
    }

    private static async ValueTask WriteSourceTextAsync(
        string filePath,
        SourceText source,
        CancellationToken cancellationToken)
    {
        var encoding = source.Encoding ?? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        await using var stream = File.Create(filePath);
        await using var writer = new StreamWriter(stream, encoding);
        await writer.WriteAsync(source.ToString().AsMemory(), cancellationToken)
            .ConfigureAwait(false);
    }

    private static class ProjectFileEditor
    {
        public static bool EnsureCompileIncluded(string projectPath, string filePath)
        {
            var document = Load(projectPath);
            var root = document.Root ?? throw new InvalidOperationException(projectPath);
            var ns = root.Name.Namespace;
            var relative = RelativeCompilePath(projectPath, filePath);
            if (HasCompileInclude(root, ns, relative))
                return false;

            var itemGroup = root.Elements(ns + "ItemGroup")
                .FirstOrDefault(group => group.Elements(ns + "Compile").Any())
                ?? new XElement(ns + "ItemGroup");
            if (itemGroup.Parent is null)
                root.Add(itemGroup);

            itemGroup.Add(new XElement(ns + "Compile", new XAttribute("Include", relative)));
            Save(projectPath, document);
            return true;
        }

        public static bool RemoveCompileIncluded(string projectPath, string filePath)
        {
            var document = Load(projectPath);
            var root = document.Root ?? throw new InvalidOperationException(projectPath);
            var ns = root.Name.Namespace;
            var relative = RelativeCompilePath(projectPath, filePath);
            var matches = root
                .Descendants(ns + "Compile")
                .Where(element =>
                    string.Equals(
                        NormalizeCompilePath((string?)element.Attribute("Include")),
                        NormalizeCompilePath(relative),
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length == 0)
                return false;

            foreach (var match in matches)
                match.Remove();
            Save(projectPath, document);
            return true;
        }

        private static bool HasCompileInclude(
            XElement root,
            XNamespace ns,
            string relative) =>
            root.Descendants(ns + "Compile")
                .Any(element =>
                    string.Equals(
                        NormalizeCompilePath((string?)element.Attribute("Include")),
                        NormalizeCompilePath(relative),
                        StringComparison.OrdinalIgnoreCase));

        private static XDocument Load(string path) =>
            XDocument.Load(path, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);

        private static void Save(string path, XDocument document) =>
            document.Save(path, SaveOptions.DisableFormatting);

        private static string RelativeCompilePath(string projectPath, string filePath) =>
            Path.GetRelativePath(
                    Path.GetDirectoryName(projectPath)!,
                    filePath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Replace(Path.AltDirectorySeparatorChar, '/');

        private static string NormalizeCompilePath(string? path) =>
            (path ?? string.Empty)
                .Replace('\\', '/')
                .TrimStart('.', '/');
    }
}
