using Microsoft.CodeAnalysis.Text;
using Novolis.Tools.CodeLayout;
using TUnit.Core;

namespace Novolis.Tools.CodeLayout.Unit;

public sealed class CodeLayoutTests
{
    [Test]
    public async Task Analyze_FindsExtraTypes_AndKeepsFileNamedType()
    {
        var source = SourceText.From("""
            namespace Demo;

            public sealed class Keeper { }
            public sealed class Extra { }
            """);

        var analysis = CodeLayoutSyntax.Analyze(
            Path.Combine(Path.GetTempPath(), "Keeper.cs"),
            source);
        var primary = CodeLayoutSyntax.SelectPrimaryType(
            Path.Combine(Path.GetTempPath(), "Keeper.cs"),
            analysis.Types);

        await Assert.That(analysis.Types.Count).IsEqualTo(2);
        await Assert.That(primary).IsEqualTo("Demo.Keeper");
        await Assert.That(analysis.Types.Select(type => type.FullName))
            .Contains("Demo.Extra");
        await Assert.That(analysis.DeletionCandidate).IsFalse();
    }

    [Test]
    public async Task Analyze_TreatsPartialsAsOneType()
    {
        var analysis = CodeLayoutSyntax.Analyze(
            "Keeper.cs",
            SourceText.From("""
                public partial class Keeper { }
                public partial class Keeper { }
                """));

        await Assert.That(analysis.Types.Count).IsEqualTo(1);
        await Assert.That(analysis.TypeLessKind).IsEqualTo("none");
        await Assert.That(analysis.DeletionCandidate).IsFalse();
    }

    [Test]
    public async Task Analyze_MapsTypeLessFiles_WithoutMarkingUsingOnlyFilesForDeletion()
    {
        var usingOnly = CodeLayoutSyntax.Analyze(
            "Imports.cs",
            SourceText.From("global using System;\n"));
        var empty = CodeLayoutSyntax.Analyze(
            "Placeholder.cs",
            SourceText.From("// placeholder\n"));
        var statements = CodeLayoutSyntax.Analyze(
            "Program.cs",
            SourceText.From("Console.WriteLine(\"hello\");\n"));
        var nullable = CodeLayoutSyntax.Analyze(
            "Nullable.cs",
            SourceText.From("#nullable enable\n"));

        await Assert.That(usingOnly.Types).IsEmpty();
        await Assert.That(usingOnly.TypeLessKind).IsEqualTo("using-only-global");
        await Assert.That(usingOnly.DeletionCandidate).IsFalse();

        await Assert.That(empty.TypeLessKind).IsEqualTo("empty-or-comments");
        await Assert.That(empty.DeletionCandidate).IsTrue();

        await Assert.That(statements.TypeLessKind).IsEqualTo("top-level-statements");
        await Assert.That(statements.DeletionCandidate).IsFalse();

        await Assert.That(nullable.TypeLessKind).IsEqualTo("directives-only");
        await Assert.That(nullable.DeletionCandidate).IsFalse();
    }

    [Test]
    public async Task KeepTypes_RemovesOtherTypes_ButPreservesRegularUsings()
    {
        var analysis = CodeLayoutSyntax.Analyze(
            "Keeper.cs",
            SourceText.From("""
                global using System;
                using System.Collections.Generic;

                [assembly: CLSCompliant(false)]

                namespace Demo
                {
                    public sealed class Keeper { }
                    public sealed class Extra { }
                }
                """));

        var root = CodeLayoutSyntax.KeepTypes(
            analysis,
            new HashSet<string>(StringComparer.Ordinal) { "Demo.Extra" },
            stripFileMetadata: true);
        var text = root.ToFullString();

        await Assert.That(text).Contains("class Extra");
        await Assert.That(text).Contains("using System.Collections.Generic");
        await Assert.That(text.Contains("class Keeper", StringComparison.Ordinal)).IsFalse();
        await Assert.That(text.Contains("global using", StringComparison.Ordinal)).IsFalse();
        await Assert.That(text.Contains("assembly:", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    public async Task Fixer_SplitsExtraType_AndDeletesOnlyWhenRequested()
    {
        var root = Path.Combine(Path.GetTempPath(), "novolis-code-layout-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var sourcePath = Path.Combine(root, "Keeper.cs");
            var emptyPath = Path.Combine(root, "Placeholder.cs");
            await File.WriteAllTextAsync(sourcePath, """
                using System;

                namespace Demo;

                public sealed class Keeper { }
                public sealed class Extra { }
                """);
            await File.WriteAllTextAsync(emptyPath, "// remove me\n");

            var sourceAnalysis = CodeLayoutSyntax.Analyze(
                sourcePath,
                SourceText.From(await File.ReadAllTextAsync(sourcePath)));
            var emptyAnalysis = CodeLayoutSyntax.Analyze(
                emptyPath,
                SourceText.From(await File.ReadAllTextAsync(emptyPath)));
            var projectPath = Path.Combine(root, "Demo.csproj");
            var project = new CodeLayoutProject(
                projectPath,
                "Demo",
                true,
                false,
                0,
                [
                    ToFile(projectPath, sourcePath, sourceAnalysis),
                    ToFile(projectPath, emptyPath, emptyAnalysis),
                ],
                []);
            var report = new CodeLayoutReport(
                Path.Combine(root, "Demo.slnx"),
                [project],
                []);

            var tracker = new RecordingGitFileTracker();
            var result = await new CodeLayoutFixer(tracker).ApplyAsync(report, delete: false);
            await Assert.That(result.FilesDeleted).IsEqualTo(0);
            await Assert.That(File.Exists(Path.Combine(root, "Extra.cs"))).IsTrue();
            await Assert.That(File.Exists(emptyPath)).IsTrue();
            await Assert.That(tracker.FilePaths)
                .Contains(Path.Combine(root, "Extra.cs"));
            await Assert.That(result.Changes.Any(change => change.Kind == "git-staged"))
                .IsTrue();

            var deleteResult = await new CodeLayoutFixer().ApplyAsync(report, delete: true);
            await Assert.That(deleteResult.FilesDeleted).IsEqualTo(1);
            await Assert.That(File.Exists(emptyPath)).IsFalse();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static CodeLayoutFile ToFile(
        string projectPath,
        string filePath,
        CodeLayoutSyntaxAnalysis analysis)
    {
        var primary = CodeLayoutSyntax.SelectPrimaryType(filePath, analysis.Types);
        var types = analysis.Types
            .Select(type => new CodeLayoutType(
                type.FullName,
                type.Name,
                type.Namespace,
                type.Kind,
                type.Declarations.Count))
            .ToArray();
        return new CodeLayoutFile(
            projectPath,
            filePath,
            Path.GetFileName(filePath),
            false,
            false,
            types,
            primary is null ? null : analysis.Types.Single(type => type.Key == primary).FullName,
            analysis.Types
                .Where(type => primary is not null && type.Key != primary)
                .Select(type => type.FullName)
                .ToArray(),
            analysis.Types.Count == 0,
            analysis.Types.Count == 0 ? analysis.TypeLessKind : "none",
            analysis.DeletionCandidate,
            analysis.DeletionReason);
    }

    private sealed class RecordingGitFileTracker : IGitFileTracker
    {
        public List<string> FilePaths { get; } = [];

        public ValueTask<GitTrackingResult> TrackAsync(
            IReadOnlyCollection<string> filePaths,
            CancellationToken cancellationToken)
        {
            FilePaths.AddRange(filePaths);
            return ValueTask.FromResult(new GitTrackingResult(
                [new CodeLayoutChange("git-staged", filePaths.First(), "staged")],
                []));
        }
    }
}
