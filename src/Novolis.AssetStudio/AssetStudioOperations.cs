using System.CommandLine;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Novolis.AssetStudio.Core;
using Novolis.AssetStudio.Export;
using Novolis.Visual.Baking;
using Novolis.Visual.CodeGen;
using Novolis.Visual.Compile;
using Novolis.Visual.Definitions;
using Novolis.Visual.Assets;
using Novolis.Visual.Materials;
using Novolis.Visual.Presets;
using Novolis.AssetStudio.Import;
using Novolis.WorkflowEngine.Graph.Export.Mermaid;
using Novolis.WorkflowEngine.Graph.Export.Svg;
using Novolis.WorkflowEngine.Graph.Layout;
using WorkflowGraph = Novolis.WorkflowEngine.Graph;

namespace Novolis.AssetStudio.Cli;

internal static class AssetStudioOperations
{
    private const int Success = 0;
    private const int DiagnosticFailure = 1;
    private const int UsageFailure = 2;
    private const int CancelledFailure = 3;
    private const int InternalFailure = 4;
    private const int MaximumBakeDimension = 4096;
    private const long MaximumBakePixels = 16_777_216;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.Default,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static async Task<int> RunSafeAsync(
        ParseResult parseResult,
        Option<bool> jsonOption,
        CancellationToken cancellationToken,
        Func<int> operation)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return operation();
        }
        catch (OperationCanceledException)
        {
            return WriteFailure(
                parseResult,
                jsonOption,
                CancelledFailure,
                "cancelled",
                "The operation was cancelled.");
        }
        catch (CliInputException exception)
        {
            return WriteFailure(
                parseResult,
                jsonOption,
                UsageFailure,
                "input",
                exception.Message);
        }
        catch (JsonException exception)
        {
            return WriteFailure(
                parseResult,
                jsonOption,
                UsageFailure,
                "json",
                exception.Message);
        }
        catch (IOException exception)
        {
            return WriteFailure(
                parseResult,
                jsonOption,
                UsageFailure,
                "io",
                exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            return WriteFailure(
                parseResult,
                jsonOption,
                UsageFailure,
                "access",
                exception.Message);
        }
        catch (Exception exception)
        {
            return WriteFailure(
                parseResult,
                jsonOption,
                InternalFailure,
                "internal",
                exception.Message);
        }
    }

    public static int Validate(
        ParseResult parseResult,
        Argument<string> recipeArgument,
        Option<string> targetOption,
        Option<bool> strictOption,
        bool json)
    {
        var loaded = LoadRecipe(parseResult.GetValue(recipeArgument));
        var target = ParseTarget(parseResult.GetValue(targetOption));
        var strict = parseResult.GetValue(strictOption);
        var compilation = Compile(loaded.Recipe, target, strict);
        var report = CreateCompilationReport(loaded, target, compilation, strict);

        WriteResult(
            json,
            report,
            () => WriteCompilationText(report));
        return HasFailure(compilation, strict)
            ? DiagnosticFailure
            : Success;
    }

    public static int Inspect(
        ParseResult parseResult,
        Argument<string> recipeArgument,
        Option<string> targetOption,
        Option<bool> capabilitiesOption,
        Option<bool> strictOption,
        bool json)
    {
        var loaded = LoadRecipe(parseResult.GetValue(recipeArgument));
        var target = ParseTarget(parseResult.GetValue(targetOption));
        var strict = parseResult.GetValue(strictOption);
        var compilation = Compile(loaded.Recipe, target, strict);
        var includeCapabilities = parseResult.GetValue(capabilitiesOption);
        var report = new InspectReport(
            !HasFailure(compilation, strict),
            loaded.Recipe.RecipeId,
            target.Name,
            loaded.State.Fingerprint,
            compilation.SemanticFingerprint,
            loaded.Recipe.Nodes
                .OrderBy(node => node.Id)
                .Select(node => new NodeReport(
                    node.Id.ToString(),
                    node.TypeId,
                    node.Version,
                    node.IsUnknown))
                .ToArray(),
            loaded.Recipe.Outputs
                .OrderBy(output => output.Id, StringComparer.Ordinal)
                .Select(output => new OutputReport(
                    output.Id,
                    output.Name,
                    output.Source.NodeId.ToString(),
                    output.Source.PortId.Value,
                    output.Required))
                .ToArray(),
            includeCapabilities
                ? MapCapabilities(compilation.Capabilities)
                : [],
            includeCapabilities,
            MapDiagnostics(compilation));

        WriteResult(
            json,
            report,
            () => WriteInspectText(report));
        return report.Ok ? Success : DiagnosticFailure;
    }

    public static int NewTemplate(
        ParseResult parseResult,
        Argument<string> templateArgument,
        Option<string> outputOption,
        bool json)
    {
        var id = parseResult.GetValue(templateArgument);
        var template = VisualPresetCatalog.CreateDefault().Find(id ?? string.Empty);
        if (template is null)
        {
            throw new CliInputException(
                $"Unknown template '{id}'. Use lightsaber-blade, neon-sign, glass, or orbital-reusable-rocket.");
        }

        var output = RequireOutputFile(parseResult.GetValue(outputOption));
        WriteUtf8(output, AssetRecipeSerializer.Serialize(template.Asset));
        var report = new NewTemplateReport(
            true,
            template.Id,
            template.Asset.AssetId,
            Path.GetFileName(output),
            template.Asset.Parameters.Length,
            template.Materials.Length);
        WriteResult(
            json,
            report,
            () =>
            {
                Console.WriteLine($"template     {report.TemplateId}");
                Console.WriteLine($"asset        {report.AssetId}");
                Console.WriteLine($"output       {report.OutputFile}");
                Console.WriteLine($"parameters   {report.ParameterCount.ToString(CultureInfo.InvariantCulture)}");
            });
        return Success;
    }

    public static int InspectParameters(
        ParseResult parseResult,
        Argument<string> sourceArgument,
        bool json)
    {
        var path = RequireInputFile(
            parseResult.GetValue(sourceArgument),
            "material or asset",
            ".json",
            ".novasset.json",
            ".novmaterial.json");
        var source = File.ReadAllText(path, Encoding.UTF8);
        using var document = JsonDocument.Parse(source);
        var root = document.RootElement;
        IReadOnlyList<ParameterReport> parameters;
        string kind;
        string id;
        if (root.TryGetProperty("assetId", out _))
        {
            var asset = AssetRecipeSerializer.Deserialize(
                source,
                VisualCompiler.CreateDefaultRegistry());
            kind = "asset";
            id = asset.AssetId;
            parameters = asset.Parameters
                .Select(parameter => new ParameterReport(
                    parameter.Id,
                    parameter.Name,
                    parameter.Editor.Group,
                    parameter.Editor.Control.ToString(),
                    parameter.DefaultValue.Kind.ToString(),
                    parameter.Editor.UnitLabel))
                .ToArray();
        }
        else if (root.TryGetProperty("materialId", out _))
        {
            var material = MaterialGraphSerializer.Deserialize(
                source,
                VisualCompiler.CreateDefaultRegistry());
            kind = "material";
            id = material.MaterialId;
            parameters = material.Parameters
                .Select(parameter => new ParameterReport(
                    parameter.Id,
                    parameter.Name,
                    parameter.Editor.Group,
                    parameter.Editor.Control.ToString(),
                    parameter.DefaultValue.Kind.ToString(),
                    parameter.Editor.UnitLabel))
                .ToArray();
        }
        else
        {
            throw new CliInputException(
                "The source must contain either an assetId or materialId.");
        }

        var report = new ParametersReport(
            true,
            kind,
            id,
            parameters);
        WriteResult(
            json,
            report,
            () =>
            {
                Console.WriteLine($"kind         {report.Kind}");
                Console.WriteLine($"id           {report.Id}");
                foreach (var parameter in report.Parameters)
                {
                    Console.WriteLine(
                        $"parameter    {parameter.Id} · {parameter.Name} · " +
                        $"{parameter.Control} · {parameter.Kind}");
                }
            });
        return Success;
    }

    public static int Preview(
        ParseResult parseResult,
        Argument<string> recipeArgument,
        Option<string> targetOption,
        Option<string> outputOption,
        Option<int> widthOption,
        Option<int> heightOption,
        Option<bool> strictOption,
        bool json)
    {
        var loaded = LoadRecipe(parseResult.GetValue(recipeArgument));
        var target = ParseTarget(parseResult.GetValue(targetOption));
        var strict = parseResult.GetValue(strictOption);
        var compilation = Compile(loaded.Recipe, target, strict);
        var outputDirectory = RequireOutputDirectory(
            parseResult.GetValue(outputOption));
        var width = parseResult.GetValue(widthOption);
        var height = parseResult.GetValue(heightOption);
        ValidateDimensions(width, height);
        var diagnostics = MapDiagnostics(compilation);
        if (HasFailure(compilation, strict))
        {
            var failed = new PreviewReport(
                false,
                loaded.Recipe.RecipeId,
                target.Name,
                null,
                null,
                diagnostics);
            WriteResult(
                json,
                failed,
                () => WriteDiagnosticsText(diagnostics));
            return DiagnosticFailure;
        }

        var result = new VisualBaker().Bake(
            compilation,
            new VisualBakeOptions
            {
                Height = height,
                Target = VisualBakeTarget.SurfaceColor,
                Width = width,
            });
        WriteBytes(Path.Combine(outputDirectory, "preview.png"), result.PngBytes);
        WriteJson(
            Path.Combine(outputDirectory, "preview.json"),
            result.Manifest);
        var report = new PreviewReport(
            true,
            loaded.Recipe.RecipeId,
            target.Name,
            "preview.png",
            result.Manifest.ContentSha256,
            diagnostics);
        WriteResult(
            json,
            report,
            () =>
            {
                Console.WriteLine($"recipe       {report.RecipeId}");
                Console.WriteLine($"target       {report.Target}");
                Console.WriteLine($"preview      {report.PreviewFile}");
                Console.WriteLine($"sha256       {report.ContentSha256}");
                WriteDiagnosticsText(report.Diagnostics);
            });
        return Success;
    }

    public static int Export(
        ParseResult parseResult,
        Argument<string> recipeArgument,
        Option<string> profileOption,
        Option<string> outputOption,
        Option<bool> strictOption,
        bool json)
    {
        var path = RequireInputFile(
            parseResult.GetValue(recipeArgument),
            "recipe or module",
            ".json",
            ".novvisual.json",
            ".novasset.json",
            ".novmaterial.json");
        var source = File.ReadAllText(path, Encoding.UTF8);
        using var sourceDocument = JsonDocument.Parse(source);
        var root = sourceDocument.RootElement;
        AssetRecipe? asset = null;
        MaterialGraph? material = null;
        VisualRecipe recipe;
        if (root.TryGetProperty("assetId", out _))
        {
            asset = AssetRecipeSerializer.Deserialize(
                source,
                VisualCompiler.CreateDefaultRegistry());
            recipe = asset.Graph;
        }
        else if (root.TryGetProperty("materialId", out _))
        {
            material = MaterialGraphSerializer.Deserialize(
                source,
                VisualCompiler.CreateDefaultRegistry());
            recipe = material.Recipe;
        }
        else
        {
            recipe = VisualRecipeSerializer.Deserialize(
                source,
                VisualCompiler.CreateDefaultRegistry());
        }

        var loaded = new LoadedRecipe(
            path,
            recipe,
            new AssetStudioSemanticState(recipe));
        var profile = ParseExportProfile(
            parseResult.GetValue(profileOption));
        var outputDirectory = RequireOutputDirectory(
            parseResult.GetValue(outputOption));
        var exporter = new AssetStudioExporter();
        var result = exporter.Export(
            new AssetStudioExportRequest(
                AssetStudioDocument.Create(loaded.Recipe),
                profile,
                new AssetStudioExportOptions
                {
                    Bake = new VisualBakeOptions
                    {
                        Target = VisualBakeTarget.SurfaceColor,
                    },
                },
                material,
                asset));
        foreach (var artifact in result.Artifacts)
        {
            WriteBytes(
                Path.Combine(
                    outputDirectory,
                    artifact.LogicalPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)),
                artifact.Content.ToArray());
        }

        WriteUtf8(
            Path.Combine(outputDirectory, "assetstudio-export.json"),
            result.Manifest.ToCanonicalJson());
        var report = new ExportReport(
            result.Success,
            loaded.Recipe.RecipeId,
            profile.ToString(),
            result.Artifacts.Length,
            result.Manifest.Sha256,
            MapExportDiagnostics(result.Report));
        WriteResult(
            json,
            report,
            () =>
            {
                Console.WriteLine($"recipe       {report.RecipeId}");
                Console.WriteLine($"profile      {report.Profile}");
                Console.WriteLine($"artifacts    {report.ArtifactCount.ToString(CultureInfo.InvariantCulture)}");
                Console.WriteLine($"manifest     {report.ManifestSha256}");
                WriteDiagnosticsText(report.Diagnostics);
            });
        return result.Success ? Success : DiagnosticFailure;
    }

    public static int Graph(
        ParseResult parseResult,
        Argument<string> recipeArgument,
        Option<string> formatOption,
        Option<string> outputOption,
        Option<bool> strictOption,
        bool json)
    {
        var loaded = LoadRecipe(parseResult.GetValue(recipeArgument));
        var target = ParseTarget("cpu");
        var strict = parseResult.GetValue(strictOption);
        var compilation = Compile(loaded.Recipe, target, strict);
        var format = ParseGraphFormat(parseResult.GetValue(formatOption));
        var output = RequireOutputFile(parseResult.GetValue(outputOption));
        var graph = CreateGraph(loaded.Recipe);
        var layout = new LayeredGraphLayout().Layout(
            graph,
            node => node.Value.Label);
        var content = format switch
        {
            GraphFormat.Mermaid => MermaidGraphExporter.Export(
                graph,
                layout,
                node => node.Value.Label),
            GraphFormat.Svg => SvgGraphExporter.Export(layout),
            _ => throw new InvalidOperationException("The graph format parser returned an invalid value."),
        };

        var report = new GraphReport(
            !HasFailure(compilation, strict),
            loaded.Recipe.RecipeId,
            loaded.State.Fingerprint,
            format.ToString().ToLowerInvariant(),
            graph.Nodes.Count,
            graph.Edges.Count,
            Path.GetFileName(output),
            MapDiagnostics(compilation));
        if (report.Ok)
        {
            WriteUtf8(output, content);
        }

        WriteResult(
            json,
            report,
            () => WriteGraphText(report));
        return report.Ok ? Success : DiagnosticFailure;
    }

    public static int Generate(
        ParseResult parseResult,
        Argument<string> recipeArgument,
        Option<string> targetOption,
        Option<string> outputOption,
        Option<string> namespaceOption,
        Option<string> typeNameOption,
        Option<string> modeOption,
        Option<bool> strictOption,
        bool json)
    {
        var loaded = LoadRecipe(parseResult.GetValue(recipeArgument));
        var target = ParseTarget(parseResult.GetValue(targetOption));
        if (target.CompilerTarget != VisualCompileTarget.CSharp)
        {
            throw new CliInputException(
                "generate only supports --target csharp.");
        }

        var strict = parseResult.GetValue(strictOption);
        var compilation = Compile(loaded.Recipe, target, strict);
        var diagnostics = MapDiagnostics(compilation);
        if (HasFailure(compilation, strict))
        {
            var failed = new GenerateReport(
                false,
                loaded.Recipe.RecipeId,
                loaded.State.Fingerprint,
                compilation.SemanticFingerprint,
                target.Name,
                null,
                null,
                null,
                false,
                [],
                diagnostics);
            WriteResult(
                json,
                failed,
                () => WriteGenerateText(failed));
            return DiagnosticFailure;
        }

        var mode = ParseCodeGenerationMode(parseResult.GetValue(modeOption));
        var source = new VisualCSharpGenerator().Generate(
            compilation,
            new VisualCodeGenerationOptions
            {
                Namespace = parseResult.GetValue(namespaceOption)
                    ?? "Generated.AssetStudio",
                TypeName = parseResult.GetValue(typeNameOption)
                    ?? "GeneratedVisual",
                Mode = mode,
            });
        var closure = CodeOnlyClosureVerifier.Verify([source]);
        var outputDirectory = RequireOutputDirectory(parseResult.GetValue(outputOption));
        var sourceFile = source.TypeName + ".g.cs";
        var manifestFile = "assetstudio-generate.json";
        var report = new GenerateReport(
            closure.IsClosed,
            loaded.Recipe.RecipeId,
            loaded.State.Fingerprint,
            compilation.SemanticFingerprint,
            target.Name,
            source.TypeName,
            mode.ToString(),
            source.SourceSha256,
            closure.IsClosed,
            closure.Violations.Order(StringComparer.Ordinal).ToArray(),
            diagnostics,
            sourceFile,
            manifestFile);

        WriteUtf8(Path.Combine(outputDirectory, sourceFile), source.Source);
        WriteJson(Path.Combine(outputDirectory, manifestFile), report);
        WriteResult(
            json,
            report,
            () => WriteGenerateText(report));
        return report.Ok ? Success : DiagnosticFailure;
    }

    public static int Bake(
        ParseResult parseResult,
        Argument<string> recipeArgument,
        Option<string> targetOption,
        Option<string> outputOption,
        Option<int> widthOption,
        Option<int> heightOption,
        Option<int> seedOption,
        Option<string[]> assetOption,
        Option<bool> strictOption,
        bool json)
    {
        var loaded = LoadRecipe(parseResult.GetValue(recipeArgument));
        var strict = parseResult.GetValue(strictOption);
        var compilation = Compile(loaded.Recipe, ParseTarget("bake"), strict);
        var bakeTarget = ParseBakeTarget(parseResult.GetValue(targetOption));
        var width = parseResult.GetValue(widthOption);
        var height = parseResult.GetValue(heightOption);
        ValidateDimensions(width, height);
        var outputDirectory = RequireOutputDirectory(parseResult.GetValue(outputOption));
        var diagnostics = MapDiagnostics(compilation);
        var assets = LoadImageAssets(parseResult.GetValue(assetOption) ?? []);

        if (HasFailure(compilation, strict))
        {
            var failed = new BakeReport(
                false,
                loaded.Recipe.RecipeId,
                loaded.State.Fingerprint,
                compilation.SemanticFingerprint,
                bakeTarget.Name,
                width,
                height,
                parseResult.GetValue(seedOption),
                [],
                diagnostics,
                null);
            WriteResult(
                json,
                failed,
                () => WriteBakeText(failed));
            return DiagnosticFailure;
        }

        var artifacts = new List<BakeArtifactReport>();
        if (bakeTarget.Name == "sprite-atlas" && assets.Count > 0)
        {
            var atlas = new VisualSpriteAtlasPacker().Bake(assets);
            var atlasFile = bakeTarget.Artifacts[0].FileName;
            WriteBytes(Path.Combine(outputDirectory, atlasFile), atlas.PngBytes);
            var atlasManifest = Path.ChangeExtension(atlasFile, ".json");
            WriteUtf8(
                Path.Combine(outputDirectory, atlasManifest),
                SerializeAtlasManifest(atlas, assets));
            artifacts.Add(
                new BakeArtifactReport(
                    "sprite-atlas",
                    VisualBakeTarget.SpriteAtlas.ToString(),
                    atlasFile,
                    Convert.ToHexString(
                            System.Security.Cryptography.SHA256.HashData(
                                atlas.PngBytes))
                        .ToLowerInvariant(),
                    atlas.PngBytes.Length));
        }

        var baker = new VisualBaker();
        foreach (var specification in bakeTarget.Artifacts.Where(
                     specification =>
                         !(bakeTarget.Name == "sprite-atlas" && assets.Count > 0)))
        {
            var result = baker.Bake(
                compilation,
                new VisualBakeOptions
                {
                    Height = height,
                    Seed = parseResult.GetValue(seedOption),
                    Target = specification.Target,
                    Width = width,
                });
            var fileName = specification.FileName;
            WriteBytes(Path.Combine(outputDirectory, fileName), result.PngBytes);
            artifacts.Add(
                new BakeArtifactReport(
                    specification.Name,
                    result.Manifest.Target.ToString(),
                    fileName,
                    result.Manifest.ContentSha256,
                    result.PngBytes.Length));
        }

        var report = new BakeReport(
            true,
            loaded.Recipe.RecipeId,
            loaded.State.Fingerprint,
            compilation.SemanticFingerprint,
            bakeTarget.Name,
            width,
            height,
            parseResult.GetValue(seedOption),
            artifacts.OrderBy(artifact => artifact.Name, StringComparer.Ordinal).ToArray(),
            diagnostics,
            "assetstudio-bake.json");
        WriteJson(Path.Combine(outputDirectory, report.ManifestFile!), report);
        WriteResult(
            json,
            report,
            () => WriteBakeText(report));
        return Success;
    }

    private static IReadOnlyList<VisualImageAsset> LoadImageAssets(
        IEnumerable<string> rawPaths)
    {
        var results = new List<VisualImageAsset>();
        foreach (var rawPath in rawPaths)
        {
            var path = RequireInputFile(
                rawPath,
                "image asset",
                ".png",
                ".jpg",
                ".jpeg",
                ".webp");
            var imported = AssetStudioImageImporter.Import(
                new AssetStudioImageImportRequest(
                    Path.GetFileName(path),
                    File.ReadAllBytes(path)));
            if (!imported.IsSuccess || imported.Asset is null)
            {
                throw new CliInputException(
                    string.Join(
                        Environment.NewLine,
                        imported.Diagnostics.Select(diagnostic =>
                            $"{diagnostic.Code}: {diagnostic.Message}")));
            }

            if (results.Any(asset =>
                    string.Equals(
                        asset.AssetId,
                        imported.Asset.AssetId,
                        StringComparison.Ordinal)))
            {
                throw new CliInputException(
                    $"Image asset id '{imported.Asset.AssetId}' is duplicated.");
            }

            results.Add(imported.Asset);
        }

        return results
            .OrderBy(asset => asset.AssetId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string SerializeAtlasManifest(
        VisualSpriteAtlasBakeResult atlas,
        IEnumerable<VisualImageAsset> assets)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("height", atlas.Image.Height);
            writer.WriteStartObject("assets");
            foreach (var asset in assets.OrderBy(
                         asset => asset.AssetId,
                         StringComparer.Ordinal))
            {
                writer.WriteString(asset.AssetId, asset.SourceSha256);
            }

            writer.WriteEndObject();
            writer.WriteStartArray("frames");
            foreach (var frame in atlas.Frames)
            {
                writer.WriteStartObject();
                writer.WriteString("assetId", frame.AssetId);
                writer.WriteNumber("durationSeconds", frame.DurationSeconds);
                writer.WriteNumber("height", frame.Height);
                writer.WriteNumber("pivotX", frame.PivotX);
                writer.WriteNumber("pivotY", frame.PivotY);
                writer.WriteString("sourceFrame", frame.SourceFrame);
                writer.WriteNumber("width", frame.Width);
                writer.WriteNumber("x", frame.X);
                writer.WriteNumber("y", frame.Y);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteNumber("width", atlas.Image.Width);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static int Build(
        ParseResult parseResult,
        Argument<string> projectArgument,
        Option<string> configurationOption,
        Option<string?> outputOption,
        Option<bool> strictOption,
        bool json)
    {
        var project = LoadProject(parseResult.GetValue(projectArgument));
        var configuration = ParseConfiguration(parseResult.GetValue(configurationOption));
        var strict = parseResult.GetValue(strictOption);
        var outputDirectoryValue = parseResult.GetValue(outputOption);
        var outputDirectory = string.IsNullOrWhiteSpace(outputDirectoryValue)
            ? null
            : RequireOutputDirectory(outputDirectoryValue);
        var recipeReports = new List<BuildRecipeReport>();
        var generated = new List<(string RelativePath, string Source)>();
        var entries = project.RecipePaths
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        for (var index = 0; index < entries.Length; index++)
        {
            var relativePath = entries[index];
            var recipePath = ResolveProjectPath(project.Directory, relativePath);
            var loaded = LoadRecipeFromPath(recipePath);
            var compilation = Compile(loaded.Recipe, ParseTarget("csharp"), strict);
            var diagnostics = MapDiagnostics(compilation);
            string? sourceFile = null;
            var codeOnly = false;
            var violations = Array.Empty<string>();
            if (!HasFailure(compilation, strict))
            {
                var source = new VisualCSharpGenerator().Generate(
                    compilation,
                    new VisualCodeGenerationOptions
                    {
                        Namespace = "Generated.Visuals",
                        TypeName = "Generated_" + SafeFileStem(loaded.Recipe.RecipeId),
                        Mode = VisualCodeGenerationMode.Specialized,
                    });
                var closure = CodeOnlyClosureVerifier.Verify([source]);
                codeOnly = closure.IsClosed;
                violations = closure.Violations.Order(StringComparer.Ordinal).ToArray();
                if (outputDirectory is not null)
                {
                    sourceFile = $"{index + 1:D4}-{SafeFileStem(Path.GetFileNameWithoutExtension(relativePath))}.g.cs";
                    WriteUtf8(Path.Combine(outputDirectory, sourceFile), source.Source);
                    generated.Add((sourceFile, source.Source));
                }
            }

            recipeReports.Add(
                new BuildRecipeReport(
                    relativePath,
                    loaded.Recipe.RecipeId,
                    loaded.State.Fingerprint,
                    compilation.SemanticFingerprint,
                    !HasFailure(compilation, strict) && codeOnly,
                    codeOnly,
                    sourceFile,
                    violations,
                    diagnostics));
        }

        var report = new BuildReport(
            recipeReports.All(recipe => recipe.Ok),
            project.Name,
            Path.GetFileName(project.FullPath),
            configuration,
            recipeReports,
            generated.Select(item => item.RelativePath)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            outputDirectory is null ? null : "assetstudio-build.json");
        if (outputDirectory is not null)
        {
            WriteJson(
                Path.Combine(outputDirectory, report.ManifestFile!),
                report);
        }

        WriteResult(
            json,
            report,
            () => WriteBuildText(report));
        return report.Ok ? Success : DiagnosticFailure;
    }

    public static int Diff(
        ParseResult parseResult,
        Argument<string> leftArgument,
        Argument<string> rightArgument,
        Option<bool> semanticOption,
        bool json)
    {
        if (!parseResult.GetValue(semanticOption))
        {
            throw new CliInputException(
                "diff requires --semantic; non-semantic source execution is not supported.");
        }

        var left = LoadRecipe(parseResult.GetValue(leftArgument));
        var right = LoadRecipe(parseResult.GetValue(rightArgument));
        var changes = SemanticChanges(left.Recipe, right.Recipe);
        var report = new DiffReport(
            changes.Length == 0,
            left.Recipe.RecipeId,
            right.Recipe.RecipeId,
            left.State.Fingerprint,
            right.State.Fingerprint,
            changes);
        WriteResult(
            json,
            report,
            () => WriteDiffText(report));
        return report.Equal ? Success : DiagnosticFailure;
    }

    private static LoadedRecipe LoadRecipe(string? rawPath)
    {
        var path = RequireInputFile(
            rawPath,
            "recipe",
            ".novvisual.json",
            ".json");
        return LoadRecipeFromPath(path);
    }

    private static LoadedRecipe LoadRecipeFromPath(string path)
    {
        var json = File.ReadAllText(path, Encoding.UTF8);
        var registry = VisualCompiler.CreateDefaultRegistry();
        var recipe = VisualRecipeSerializer.Deserialize(json, registry);
        return new LoadedRecipe(
            path,
            recipe,
            new AssetStudioSemanticState(recipe));
    }

    private static ProjectManifest LoadProject(string? rawPath)
    {
        var fullPath = RequireInputFile(
            rawPath,
            "project",
            ".novassetproj",
            ".json");
        var json = File.ReadAllText(fullPath, Encoding.UTF8);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new CliInputException("A .novassetproj file must contain a JSON object.");
        }

        if (root.TryGetProperty("schemaVersion", out var schemaElement))
        {
            if (schemaElement.ValueKind != JsonValueKind.Number ||
                schemaElement.GetInt32() != 1)
            {
                throw new CliInputException(
                    "Only project schemaVersion 1 is supported.");
            }
        }

        var paths = new List<string>();
        if (root.TryGetProperty("recipes", out var recipesElement))
        {
            if (recipesElement.ValueKind != JsonValueKind.Array)
            {
                throw new CliInputException("Project property 'recipes' must be an array.");
            }

            foreach (var entry in recipesElement.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String)
                {
                    paths.Add(entry.GetString() ?? string.Empty);
                }
                else if (entry.ValueKind == JsonValueKind.Object &&
                         entry.TryGetProperty("path", out var pathElement) &&
                         pathElement.ValueKind == JsonValueKind.String)
                {
                    paths.Add(pathElement.GetString() ?? string.Empty);
                }
                else
                {
                    throw new CliInputException(
                        "Each project recipe entry must be a relative path string or an object with 'path'.");
                }
            }
        }
        else if (root.TryGetProperty("recipe", out var recipeElement) &&
                 recipeElement.ValueKind == JsonValueKind.String)
        {
            paths.Add(recipeElement.GetString() ?? string.Empty);
        }
        else
        {
            throw new CliInputException(
                "A project must declare one or more recipe paths in 'recipes'.");
        }

        if (paths.Any(string.IsNullOrWhiteSpace) ||
            paths.Distinct(StringComparer.Ordinal).Count() != paths.Count)
        {
            throw new CliInputException(
                "Project recipe paths must be non-empty and unique.");
        }

        var name = root.TryGetProperty("name", out var nameElement) &&
                   nameElement.ValueKind == JsonValueKind.String
            ? nameElement.GetString()
            : null;
        return new ProjectManifest(
            fullPath,
            Path.GetDirectoryName(fullPath)!,
            string.IsNullOrWhiteSpace(name)
                ? Path.GetFileNameWithoutExtension(fullPath)
                : name!,
            paths);
    }

    private static VisualIR Compile(
        VisualRecipe recipe,
        TargetSpec target,
        bool strict) =>
        VisualCompiler.Compile(
            recipe,
            new VisualCompileOptions(
                registry: VisualCompiler.CreateDefaultRegistry(),
                targetCapabilities: VisualTargetCapabilities.Open(),
                optimize: true,
                strict: strict,
                eliminateDeadNodes: true));

    private static CompilationReport CreateCompilationReport(
        LoadedRecipe loaded,
        TargetSpec target,
        VisualIR compilation,
        bool strict) =>
        new(
            !HasFailure(compilation, strict),
            loaded.Recipe.RecipeId,
            target.Name,
            loaded.State.Fingerprint,
            compilation.SemanticFingerprint,
            compilation.Nodes.Length,
            compilation.Outputs.Length,
            MapDiagnostics(compilation),
            MapCapabilities(compilation.Capabilities));

    private static bool HasFailure(VisualIR compilation, bool strict) =>
        !compilation.Success ||
        strict && compilation.Diagnostics.Length > 0;

    private static IReadOnlyList<DiagnosticReport> MapDiagnostics(VisualIR compilation) =>
        compilation.Diagnostics
            .OrderBy(diagnostic =>
                diagnostic.Severity == VisualDiagnosticSeverity.Error ? 0 : 1)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.NodeId?.ToString(), StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.ConnectionId?.ToString(), StringComparer.Ordinal)
            .Select(diagnostic => new DiagnosticReport(
                diagnostic.Severity.ToString().ToLowerInvariant(),
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.NodeId is { } nodeId ? nodeId.ToString() : null,
                diagnostic.ConnectionId is { } connectionId
                    ? connectionId.ToString()
                    : null))
            .ToArray();

    private static IReadOnlyList<CapabilityReport> MapCapabilities(
        VisualCapabilityReport capabilities) =>
        capabilities.Results
            .OrderBy(result => result.Id, StringComparer.Ordinal)
            .Select(result => new CapabilityReport(
                result.Id,
                result.Status.ToString().ToLowerInvariant(),
                result.Required,
                result.MinimumVersion,
                result.Message))
            .ToArray();

    private static IReadOnlyList<DiagnosticReport> MapExportDiagnostics(
        AssetStudioBuildReport report) =>
        report.Diagnostics
            .Select(diagnostic => new DiagnosticReport(
                diagnostic.Severity.ToString().ToLowerInvariant(),
                diagnostic.Code,
                diagnostic.Message,
                diagnostic.NodeId?.ToString(),
                diagnostic.ConnectionId?.ToString()))
            .ToArray();

    private static WorkflowGraph.Graph<GraphNodeValue> CreateGraph(
        VisualRecipe recipe)
    {
        var nodeIds = recipe.Nodes
            .Select(node => node.Id)
            .ToHashSet();
        var nodes = recipe.Nodes
            .OrderBy(node => node.Id)
            .Select(node => new WorkflowGraph.GraphNode<GraphNodeValue>(
                new WorkflowGraph.NodeId(node.Id.Value),
                new GraphNodeValue(
                    node.TypeId + " [" + node.Id + "]",
                    node.TypeId)))
            .ToArray();
        var edges = recipe.Connections
            .Where(connection =>
                nodeIds.Contains(connection.Source.NodeId) &&
                nodeIds.Contains(connection.Target.NodeId))
            .OrderBy(connection => connection.Id)
            .Select(connection => new WorkflowGraph.GraphEdge(
                new WorkflowGraph.EdgeId(connection.Id.Value),
                new WorkflowGraph.NodeId(connection.Source.NodeId.Value),
                new WorkflowGraph.NodeId(connection.Target.NodeId.Value),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sourcePort"] = connection.Source.PortId.Value,
                    ["targetPort"] = connection.Target.PortId.Value,
                }))
            .ToArray();
        return new WorkflowGraph.Graph<GraphNodeValue>(nodes, edges);
    }

    private static string[] SemanticChanges(
        VisualRecipe left,
        VisualRecipe right)
    {
        var leftJson = VisualRecipeSerializer.Serialize(left);
        var rightJson = VisualRecipeSerializer.Serialize(right);
        using var leftDocument = JsonDocument.Parse(leftJson);
        using var rightDocument = JsonDocument.Parse(rightJson);
        var fields = new[]
        {
            "capabilities",
            "connections",
            "dependencies",
            "nodes",
            "outputs",
            "parameters",
            "recipeId",
            "schemaVersion",
            "seedPolicy",
        };
        return fields
            .Where(field => !JsonPropertyText(leftDocument.RootElement, field)
                .Equals(
                    JsonPropertyText(rightDocument.RootElement, field),
                    StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string JsonPropertyText(JsonElement root, string name) =>
        root.TryGetProperty(name, out var element)
            ? element.GetRawText()
            : string.Empty;

    private static TargetSpec ParseTarget(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new CliInputException("A compilation target is required.");
        }

        return raw.Trim().ToLowerInvariant() switch
        {
            "cpu" or "reference" => new("cpu", VisualCompileTarget.Cpu),
            "bake" => new("bake", VisualCompileTarget.Bake),
            "pathtrace" or "path-trace" or "trace" =>
                new("path-trace", VisualCompileTarget.PathTrace),
            "raster" => new("raster", VisualCompileTarget.Raster),
            "planar" or "silk" => new("planar", VisualCompileTarget.Planar),
            "csharp" or "c#" or "code" or "codegen" =>
                new("csharp", VisualCompileTarget.CSharp),
            _ => throw new CliInputException(
                $"Unknown target '{raw}'. Use cpu, bake, path-trace, raster, planar, or csharp."),
        };
    }

    private static AssetStudioExportProfile ParseExportProfile(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "definition" => AssetStudioExportProfile.Definition,
            "material-module" or "material" =>
                AssetStudioExportProfile.MaterialModule,
            "asset-package" or "asset" =>
                AssetStudioExportProfile.AssetPackage,
            "preview-thumbnail" or "thumbnail" or "preview" =>
                AssetStudioExportProfile.PreviewThumbnail,
            "code" or "csharp" => AssetStudioExportProfile.Code,
            "bake" => AssetStudioExportProfile.Bake,
            "packed" or "pack" => AssetStudioExportProfile.Packed,
            "build" => AssetStudioExportProfile.Build,
            "documentation" or "docs" =>
                AssetStudioExportProfile.Documentation,
            _ => throw new CliInputException(
                "Unknown export profile. Use definition, material-module, " +
                "asset-package, preview-thumbnail, code, bake, packed, or build."),
        };

    private static GraphFormat ParseGraphFormat(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "mermaid" or "mmd" => GraphFormat.Mermaid,
            "svg" => GraphFormat.Svg,
            _ => throw new CliInputException(
                $"Unknown graph format '{raw}'. Use mermaid or svg."),
        };

    private static VisualCodeGenerationMode ParseCodeGenerationMode(string? raw) =>
        raw?.Trim().ToLowerInvariant() switch
        {
            "builder" => VisualCodeGenerationMode.Builder,
            "specialized" => VisualCodeGenerationMode.Specialized,
            "embedded" or "embedded-payload" or "embeddedpayload" =>
                VisualCodeGenerationMode.EmbeddedPayload,
            _ => throw new CliInputException(
                $"Unknown code mode '{raw}'. Use builder, specialized, or embedded-payload."),
        };

    private static BakeTargetSpec ParseBakeTarget(string? raw)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            "sprite-atlas" or "spriteatlas" => new(
                "sprite-atlas",
                [new("sprite-atlas", VisualBakeTarget.SpriteAtlas, "sprite-atlas.png")]),
            "maps" or "map" => new(
                "maps",
                [
                    new("emission", VisualBakeTarget.EmissionMap, "emission.png"),
                    new("normal", VisualBakeTarget.NormalMap, "normal.png"),
                    new("roughness", VisualBakeTarget.RoughnessMap, "roughness.png"),
                ]),
            "mesh" => new(
                "mesh",
                [new("mesh", VisualBakeTarget.Mesh, "mesh.png")]),
            _ => throw new CliInputException(
                $"Unknown bake target '{raw}'. Use sprite-atlas, maps, or mesh."),
        };
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width <= 0 ||
            height <= 0 ||
            width > MaximumBakeDimension ||
            height > MaximumBakeDimension ||
            (long)width * height > MaximumBakePixels)
        {
            throw new CliInputException(
                $"Bake dimensions must be between 1 and {MaximumBakeDimension} " +
                $"with at most {MaximumBakePixels.ToString(CultureInfo.InvariantCulture)} pixels.");
        }
    }

    private static string ParseConfiguration(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) ||
            raw.Any(character =>
                !char.IsLetterOrDigit(character) &&
                character is not '.' and not '-' and not '_'))
        {
            throw new CliInputException(
                "Configuration must contain only letters, digits, '.', '-', or '_'.");
        }

        return raw;
    }

    private static string ResolveProjectPath(string projectDirectory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
        {
            throw new CliInputException(
                $"Project recipe path '{relativePath}' must be relative to the project.");
        }

        var fullPath = GetFullPath(Path.Combine(projectDirectory, relativePath));
        if (!IsWithin(projectDirectory, fullPath))
        {
            throw new CliInputException(
                $"Project recipe path '{relativePath}' escapes the project directory.");
        }

        return RequireInputFile(
            fullPath,
            "project recipe",
            ".novvisual.json",
            ".json");
    }

    private static string RequireInputFile(
        string? rawPath,
        string description,
        params string[] extensions)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new CliInputException($"A {description} path is required.");
        }

        var path = GetFullPath(rawPath);
        if (!File.Exists(path) || Directory.Exists(path))
        {
            throw new CliInputException(
                $"The {description} file does not exist: {Path.GetFileName(path)}.");
        }

        if (extensions.Length > 0 &&
            !extensions.Any(extension =>
                path.EndsWith(extension, StringComparison.OrdinalIgnoreCase)))
        {
            throw new CliInputException(
                $"The {description} must use one of these extensions: " +
                string.Join(", ", extensions));
        }

        return path;
    }

    private static string RequireOutputFile(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new CliInputException("An output file path is required.");
        }

        var path = GetFullPath(rawPath);
        if (Directory.Exists(path))
        {
            throw new CliInputException("The graph output path is a directory.");
        }

        var directory = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new CliInputException("The graph output directory could not be resolved.");
        }

        Directory.CreateDirectory(directory);
        return path;
    }

    private static string RequireOutputDirectory(string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            throw new CliInputException("An output directory path is required.");
        }

        var path = GetFullPath(rawPath);
        if (File.Exists(path))
        {
            throw new CliInputException("The output directory path is a file.");
        }

        Directory.CreateDirectory(path);
        return path;
    }

    private static string GetFullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (
            exception is ArgumentException or
            NotSupportedException or
            PathTooLongException)
        {
            throw new CliInputException($"Invalid path: {path}");
        }
    }

    private static bool IsWithin(string root, string path)
    {
        var normalizedRoot = GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var normalizedPath = GetFullPath(path);
        return normalizedPath.StartsWith(
                   normalizedRoot,
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizedPath,
                   normalizedRoot.TrimEnd(Path.DirectorySeparatorChar),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeFileStem(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(
                char.IsLetterOrDigit(character) || character is '-' or '_'
                    ? character
                    : '_');
        }

        var result = builder.Length == 0 ? "asset" : builder.ToString();
        return result.Trim('.', ' ');
    }

    private static void WriteResult<T>(
        bool json,
        T value,
        Action humanWriter)
    {
        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
        }
        else
        {
            humanWriter();
        }
    }

    private static int WriteFailure(
        ParseResult parseResult,
        Option<bool> jsonOption,
        int exitCode,
        string error,
        string message)
    {
        var payload = new ErrorReport(false, error, message);
        if (parseResult.GetValue(jsonOption))
        {
            Console.Error.WriteLine(JsonSerializer.Serialize(payload, JsonOptions));
        }
        else
        {
            Console.Error.WriteLine($"error [{error}]: {message}");
        }

        return exitCode;
    }

    private static void WriteCompilationText(CompilationReport report)
    {
        Console.WriteLine($"recipe       {report.RecipeId}");
        Console.WriteLine($"target       {report.Target}");
        Console.WriteLine($"valid        {report.Ok.ToString().ToLowerInvariant()}");
        Console.WriteLine($"fingerprint  {report.Fingerprint}");
        Console.WriteLine($"recipe hash  {report.RecipeFingerprint}");
        Console.WriteLine($"nodes        {report.NodeCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"outputs      {report.OutputCount.ToString(CultureInfo.InvariantCulture)}");
        WriteDiagnosticsText(report.Diagnostics);
    }

    private static void WriteInspectText(InspectReport report)
    {
        Console.WriteLine($"recipe       {report.RecipeId}");
        Console.WriteLine($"target       {report.Target}");
        Console.WriteLine($"valid        {report.Ok.ToString().ToLowerInvariant()}");
        Console.WriteLine($"nodes        {report.Nodes.Count.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"outputs      {report.Outputs.Count.ToString(CultureInfo.InvariantCulture)}");
        if (report.CapabilitiesIncluded)
        {
            foreach (var capability in report.Capabilities)
            {
                Console.WriteLine(
                    $"capability   {capability.Id} {capability.Status}" +
                    (capability.Required ? " required" : " optional"));
            }
        }

        WriteDiagnosticsText(report.Diagnostics);
    }

    private static void WriteGraphText(GraphReport report)
    {
        Console.WriteLine($"recipe       {report.RecipeId}");
        Console.WriteLine($"format       {report.Format}");
        Console.WriteLine($"valid        {report.Ok.ToString().ToLowerInvariant()}");
        Console.WriteLine($"nodes        {report.NodeCount.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"edges        {report.EdgeCount.ToString(CultureInfo.InvariantCulture)}");
        if (report.OutputFile is not null)
        {
            Console.WriteLine($"output       {report.OutputFile}");
        }

        WriteDiagnosticsText(report.Diagnostics);
    }

    private static void WriteGenerateText(GenerateReport report)
    {
        Console.WriteLine($"recipe       {report.RecipeId}");
        Console.WriteLine($"valid        {report.Ok.ToString().ToLowerInvariant()}");
        Console.WriteLine($"code-only    {report.CodeOnly.ToString().ToLowerInvariant()}");
        if (report.SourceFile is not null)
        {
            Console.WriteLine($"source       {report.SourceFile}");
        }

        if (report.SourceSha256 is not null)
        {
            Console.WriteLine($"source hash  {report.SourceSha256}");
        }

        foreach (var violation in report.ClosureViolations)
        {
            Console.WriteLine($"violation    {violation}");
        }

        WriteDiagnosticsText(report.Diagnostics);
    }

    private static void WriteBakeText(BakeReport report)
    {
        Console.WriteLine($"recipe       {report.RecipeId}");
        Console.WriteLine($"target       {report.Target}");
        Console.WriteLine($"valid        {report.Ok.ToString().ToLowerInvariant()}");
        foreach (var artifact in report.Artifacts)
        {
            Console.WriteLine(
                $"artifact     {artifact.Name} {artifact.FileName} {artifact.ContentSha256}");
        }

        WriteDiagnosticsText(report.Diagnostics);
    }

    private static void WriteBuildText(BuildReport report)
    {
        Console.WriteLine($"project      {report.ProjectName}");
        Console.WriteLine($"configuration {report.Configuration}");
        Console.WriteLine($"valid        {report.Ok.ToString().ToLowerInvariant()}");
        foreach (var recipe in report.Recipes)
        {
            Console.WriteLine(
                $"recipe       {recipe.RelativePath} " +
                $"{recipe.Ok.ToString().ToLowerInvariant()}");
            WriteDiagnosticsText(recipe.Diagnostics);
        }
    }

    private static void WriteDiffText(DiffReport report)
    {
        Console.WriteLine($"equal        {report.Equal.ToString().ToLowerInvariant()}");
        Console.WriteLine($"left         {report.LeftFingerprint}");
        Console.WriteLine($"right        {report.RightFingerprint}");
        foreach (var change in report.ChangedSections)
        {
            Console.WriteLine($"changed      {change}");
        }
    }

    private static void WriteDiagnosticsText(
        IEnumerable<DiagnosticReport> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            var location = diagnostic.NodeId is null
                ? diagnostic.ConnectionId is null
                    ? string.Empty
                    : $" connection={diagnostic.ConnectionId}"
                : $" node={diagnostic.NodeId}";
            Console.WriteLine(
                $"diagnostic   {diagnostic.Severity} {diagnostic.Code}{location}: " +
                diagnostic.Message);
        }
    }

    private static void WriteJson(string path, object value) =>
        WriteUtf8(path, JsonSerializer.Serialize(value, JsonOptions));

    private static void WriteUtf8(string path, string value) =>
        File.WriteAllText(
            path,
            value.EndsWith('\n') ? value : value + "\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

    private static void WriteBytes(string path, byte[] bytes) =>
        WriteBytesToDisk(path, bytes);

    private static void WriteBytesToDisk(string path, byte[] bytes)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllBytes(path, bytes);
    }

    private sealed class CliInputException : Exception
    {
        public CliInputException(string message)
            : base(message)
        {
        }
    }

    private sealed record LoadedRecipe(
        string FullPath,
        VisualRecipe Recipe,
        AssetStudioSemanticState State);

    private sealed record ProjectManifest(
        string FullPath,
        string Directory,
        string Name,
        IReadOnlyList<string> RecipePaths);

    private sealed record TargetSpec(string Name, VisualCompileTarget CompilerTarget);

    private sealed record GraphNodeValue(string Label, string TypeId);

    private sealed record BakeTargetSpec(
        string Name,
        IReadOnlyList<BakeArtifactSpec> Artifacts);

    private sealed record BakeArtifactSpec(
        string Name,
        VisualBakeTarget Target,
        string FileName);

    private sealed record DiagnosticReport(
        string Severity,
        string Code,
        string Message,
        string? NodeId,
        string? ConnectionId);

    private sealed record CapabilityReport(
        string Id,
        string Status,
        bool Required,
        string? MinimumVersion,
        string? Message);

    private sealed record NodeReport(
        string Id,
        string Type,
        int Version,
        bool Unknown);

    private sealed record OutputReport(
        string Id,
        string Name,
        string NodeId,
        string PortId,
        bool Required);

    private sealed record CompilationReport(
        bool Ok,
        string RecipeId,
        string Target,
        string RecipeFingerprint,
        string Fingerprint,
        int NodeCount,
        int OutputCount,
        IReadOnlyList<DiagnosticReport> Diagnostics,
        IReadOnlyList<CapabilityReport> Capabilities);

    private sealed record InspectReport(
        bool Ok,
        string RecipeId,
        string Target,
        string RecipeFingerprint,
        string Fingerprint,
        IReadOnlyList<NodeReport> Nodes,
        IReadOnlyList<OutputReport> Outputs,
        IReadOnlyList<CapabilityReport> Capabilities,
        bool CapabilitiesIncluded,
        IReadOnlyList<DiagnosticReport> Diagnostics);

    private sealed record NewTemplateReport(
        bool Ok,
        string TemplateId,
        string AssetId,
        string OutputFile,
        int ParameterCount,
        int MaterialCount);

    private sealed record ParameterReport(
        string Id,
        string Name,
        string? Group,
        string Control,
        string Kind,
        string? Unit);

    private sealed record ParametersReport(
        bool Ok,
        string Kind,
        string Id,
        IReadOnlyList<ParameterReport> Parameters);

    private sealed record PreviewReport(
        bool Ok,
        string RecipeId,
        string Target,
        string? PreviewFile,
        string? ContentSha256,
        IReadOnlyList<DiagnosticReport> Diagnostics);

    private sealed record ExportReport(
        bool Ok,
        string RecipeId,
        string Profile,
        int ArtifactCount,
        string ManifestSha256,
        IReadOnlyList<DiagnosticReport> Diagnostics);

    private sealed record GraphReport(
        bool Ok,
        string RecipeId,
        string Fingerprint,
        string Format,
        int NodeCount,
        int EdgeCount,
        string? OutputFile,
        IReadOnlyList<DiagnosticReport> Diagnostics);

    private sealed record GenerateReport(
        bool Ok,
        string RecipeId,
        string RecipeFingerprint,
        string CompilationFingerprint,
        string Target,
        string? TypeName,
        string? Mode,
        string? SourceSha256,
        bool CodeOnly,
        IReadOnlyList<string> ClosureViolations,
        IReadOnlyList<DiagnosticReport> Diagnostics,
        string? SourceFile = null,
        string? ManifestFile = null);

    private sealed record BakeArtifactReport(
        string Name,
        string Target,
        string FileName,
        string ContentSha256,
        int ByteCount);

    private sealed record BakeReport(
        bool Ok,
        string RecipeId,
        string RecipeFingerprint,
        string CompilationFingerprint,
        string Target,
        int Width,
        int Height,
        int Seed,
        IReadOnlyList<BakeArtifactReport> Artifacts,
        IReadOnlyList<DiagnosticReport> Diagnostics,
        string? ManifestFile);

    private sealed record BuildRecipeReport(
        string RelativePath,
        string RecipeId,
        string RecipeFingerprint,
        string CompilationFingerprint,
        bool Ok,
        bool CodeOnly,
        string? SourceFile,
        IReadOnlyList<string> ClosureViolations,
        IReadOnlyList<DiagnosticReport> Diagnostics);

    private sealed record BuildReport(
        bool Ok,
        string ProjectName,
        string ProjectFile,
        string Configuration,
        IReadOnlyList<BuildRecipeReport> Recipes,
        IReadOnlyList<string> OutputFiles,
        string? ManifestFile);

    private sealed record DiffReport(
        bool Equal,
        string LeftRecipeId,
        string RightRecipeId,
        string LeftFingerprint,
        string RightFingerprint,
        IReadOnlyList<string> ChangedSections);

    private sealed record ErrorReport(bool Ok, string Error, string Message);

    private enum GraphFormat
    {
        Mermaid,
        Svg,
    }

}
