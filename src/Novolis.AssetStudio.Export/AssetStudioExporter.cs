using System.Text;
using System.Text.Json;
using Novolis.AssetStudio.Authoring;
using Novolis.Visual.Baking;
using Novolis.Visual.CodeGen;
using Novolis.Visual.Compile;
using Novolis.Visual.Definitions;
using Novolis.Visual.Assets;
using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Export;

/// <summary>
/// Produces deterministic in-memory artifacts for every Asset Studio export
/// profile.
/// </summary>
public sealed class AssetStudioExporter
{
    /// <summary>
    /// Exports one immutable document without accessing the host file system.
    /// </summary>
    public AssetStudioExportResult Export(AssetStudioExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var diagnostics = new List<AssetStudioDiagnostic>();
        var artifacts = new List<AssetStudioExportArtifact>();
        var closureViolations = new List<string>();
        var codeOnlyClosed = true;
        VisualIR? compilation = null;
        AssetStudioExportOptions options;

        try
        {
            options = request.Options;
            options.Validate();
        }
        catch (ArgumentException exception)
        {
            diagnostics.Add(Error("AST200", exception.Message));
            options = new AssetStudioExportOptions();
        }

        var recipe = request.Document.Recipe;
        var material = request.Material ?? request.Document.Material;
        var asset = request.Asset ?? request.Document.Asset;
        var fingerprint = request.Document.ContextFingerprint;
        var compileProfile = request.Profile is
            AssetStudioExportProfile.Code or
            AssetStudioExportProfile.Bake or
            AssetStudioExportProfile.Build or
            AssetStudioExportProfile.Packed or
            AssetStudioExportProfile.AssetPackage or
            AssetStudioExportProfile.PreviewThumbnail;
        if (compileProfile)
        {
            compilation = request.Document.DocumentKind switch
            {
                AssetStudioDocumentKind.Material when material is { } materialGraph =>
                    VisualMaterialCompiler.CompileMaterial(
                        materialGraph,
                        request.Document.MaterialInstance),
                AssetStudioDocumentKind.Asset when asset is { } assetRecipe =>
                    VisualMaterialCompiler.CompileAsset(
                        assetRecipe,
                        request.Document.AssetInstance),
                _ => VisualCompiler.Compile(recipe),
            };
            diagnostics.AddRange(
                compilation.Diagnostics.Select(
                    diagnostic => AssetStudioDiagnostic.From(
                        diagnostic,
                        request.Document.Revision)));
        }

        switch (request.Profile)
        {
            case AssetStudioExportProfile.Definition:
                AddTextArtifact(
                    artifacts,
                    "recipe.json",
                    "application/json",
                    AssetStudioDocumentPersistence.ToCanonicalJson(
                        request.Document));
                break;

            case AssetStudioExportProfile.MaterialModule:
                AddTextArtifact(
                    artifacts,
                    "material/recipe.json",
                    "application/json",
                    material is { } materialGraph
                        ? MaterialGraphSerializer.Serialize(materialGraph)
                        : AssetStudioDocumentPersistence.ToCanonicalJson(
                            request.Document));
                break;

            case AssetStudioExportProfile.AssetPackage:
                if (asset is { } assetRecipe)
                {
                    AddTextArtifact(
                        artifacts,
                        "asset/asset.json",
                        "application/json",
                        AssetRecipeSerializer.Serialize(assetRecipe));
                }
                else if (compilation is not null && compilation.Success)
                {
                    AddPackedArtifact(artifacts, request.Document, options);
                }

                break;

            case AssetStudioExportProfile.PreviewThumbnail:
                if (compilation is not null && compilation.Success)
                {
                    AddBakeArtifacts(artifacts, compilation, options);
                }

                break;

            case AssetStudioExportProfile.Code:
                if (compilation is not null && compilation.Success)
                {
                    AddCodeArtifacts(
                        artifacts,
                        compilation,
                        options,
                        diagnostics,
                        closureViolations,
                        ref codeOnlyClosed,
                        "code");
                    AddImageCodeArtifacts(
                        artifacts,
                        options,
                        diagnostics,
                        closureViolations,
                        ref codeOnlyClosed,
                        "code");
                }

                break;

            case AssetStudioExportProfile.Packed:
                if (compilation is not null && compilation.Success)
                {
                    AddPackedArtifact(artifacts, request.Document, options);
                }

                break;

            case AssetStudioExportProfile.Bake:
                if (compilation is not null && compilation.Success)
                {
                    AddBakeArtifacts(artifacts, compilation, options);
                }

                break;

            case AssetStudioExportProfile.Documentation:
                AddTextArtifact(
                    artifacts,
                    "documentation.md",
                    "text/markdown",
                    CreateDocumentation(request.Document, fingerprint));
                break;

            case AssetStudioExportProfile.Build:
                AddTextArtifact(
                    artifacts,
                    "build/recipe.json",
                    "application/json",
                    AssetStudioDocumentPersistence.ToCanonicalJson(
                        request.Document));
                if (compilation is not null && compilation.Success)
                {
                    AddCodeArtifacts(
                        artifacts,
                        compilation,
                        options,
                        diagnostics,
                        closureViolations,
                        ref codeOnlyClosed,
                        "build");
                    AddImageCodeArtifacts(
                        artifacts,
                        options,
                        diagnostics,
                        closureViolations,
                        ref codeOnlyClosed,
                        "build");
                }

                break;

            default:
                diagnostics.Add(
                    Error(
                        "AST201",
                        $"Unsupported export profile '{request.Profile}'."));
                break;
        }

        ValidateArtifacts(artifacts, options.Limits, diagnostics);
        var success = !diagnostics.Any(
                         diagnostic =>
                             diagnostic.Severity ==
                             AssetStudioDiagnosticSeverity.Error) &&
                     codeOnlyClosed;
        var report = new AssetStudioBuildReport(
            request.Profile,
            fingerprint,
            success,
            diagnostics,
            codeOnlyClosed,
            closureViolations,
            artifacts.Count);
        if (request.Profile == AssetStudioExportProfile.Build)
        {
            AddTextArtifact(
                artifacts,
                "build/report.json",
                "application/json",
                report.ToCanonicalJson());
            ValidateArtifacts(artifacts, options.Limits, diagnostics);
            success = !diagnostics.Any(
                           diagnostic =>
                               diagnostic.Severity ==
                               AssetStudioDiagnosticSeverity.Error) &&
                       codeOnlyClosed;
            report = new AssetStudioBuildReport(
                request.Profile,
                fingerprint,
                success,
                diagnostics,
                codeOnlyClosed,
                closureViolations,
                artifacts.Count - 1);
        }

        var manifest = new AssetStudioExportManifest(
            fingerprint,
            request.Profile,
            artifacts);
        return new AssetStudioExportResult(artifacts, manifest, report);
    }

    private static void AddCodeArtifacts(
        ICollection<AssetStudioExportArtifact> artifacts,
        VisualIR compilation,
        AssetStudioExportOptions options,
        ICollection<AssetStudioDiagnostic> diagnostics,
        ICollection<string> closureViolations,
        ref bool codeOnlyClosed,
        string prefix)
    {
        var generated = new VisualCSharpGenerator().Generate(
            compilation,
            new VisualCodeGenerationOptions
            {
                Namespace = options.CodeNamespace,
                TypeName = options.CodeTypeName,
                Mode = VisualCodeGenerationMode.Specialized,
            });
        var closure = CodeOnlyClosureVerifier.Verify([generated]);
        codeOnlyClosed = closure.IsClosed;
        foreach (var violation in closure.Violations)
        {
            closureViolations.Add(violation);
            diagnostics.Add(
                Error(
                    "AST220",
                    $"Generated code is not code-only closed: {violation}."));
        }

        AddTextArtifact(
            artifacts,
            $"{prefix}/{generated.TypeName}.cs",
            "text/x-csharp",
            generated.Source);
        AddTextArtifact(
            artifacts,
            $"{prefix}/closure.json",
            "application/json",
            SerializeClosure(closure));
    }

    private static void AddImageCodeArtifacts(
        ICollection<AssetStudioExportArtifact> artifacts,
        AssetStudioExportOptions options,
        ICollection<AssetStudioDiagnostic> diagnostics,
        ICollection<string> closureViolations,
        ref bool codeOnlyClosed,
        string prefix)
    {
        if (options.Assets.Count == 0)
        {
            return;
        }

        var generated = new VisualImageCodeGenerator().Generate(
            options.Assets,
            options.CodeNamespace,
            options.CodeTypeName + "Images");
        var closure = CodeOnlyClosureVerifier.Verify([generated]);
        codeOnlyClosed &= closure.IsClosed;
        foreach (var violation in closure.Violations)
        {
            closureViolations.Add(violation);
            diagnostics.Add(
                Error(
                    "AST221",
                    $"Generated image code is not code-only closed: {violation}."));
        }

        AddTextArtifact(
            artifacts,
            $"{prefix}/{generated.TypeName}.cs",
            "text/x-csharp",
            generated.Source);
    }

    private static void AddPackedArtifact(
        ICollection<AssetStudioExportArtifact> artifacts,
        AssetStudioDocument document,
        AssetStudioExportOptions options)
    {
        var recipe = AssetStudioDocumentPersistence.ToCanonicalJson(document);
        var header = "NOVOLIS-ASSET-PACK/1\n";
        AddArtifact(
            artifacts,
            "packed/recipe.assetpack",
            "application/octet-stream",
            Encoding.UTF8.GetBytes(header + recipe));
        foreach (var asset in options.Assets.OrderBy(
                     asset => asset.AssetId,
                     StringComparer.Ordinal))
        {
            AddArtifact(
                artifacts,
                $"packed/assets/{SafeAssetName(asset.AssetId)}.rgba",
                "application/octet-stream",
                asset.RgbaPixels);
        }
    }

    private static void AddBakeArtifacts(
        ICollection<AssetStudioExportArtifact> artifacts,
        VisualIR compilation,
        AssetStudioExportOptions options)
    {
        if (options.Bake.Target == VisualBakeTarget.SpriteAtlas &&
            options.Assets.Count > 0)
        {
            var atlas = new VisualSpriteAtlasPacker().Bake(options.Assets);
            AddArtifact(artifacts, "bake/sprite-atlas.png", "image/png", atlas.PngBytes);
            AddTextArtifact(
                artifacts,
                "bake/sprite-atlas.json",
                "application/json",
                SerializeAtlas(atlas, options.Assets));
            return;
        }

        var baked = new VisualBaker().Bake(compilation, options.Bake);
        AddArtifact(artifacts, "bake/image.png", "image/png", baked.PngBytes);
        AddTextArtifact(
            artifacts,
            "bake/manifest.json",
            "application/json",
            SerializeBakeManifest(baked.Manifest));
    }

    private static string SerializeAtlas(
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
            writer.WriteString(
                "sha256",
                Convert.ToHexString(
                        System.Security.Cryptography.SHA256.HashData(
                            atlas.PngBytes))
                    .ToLowerInvariant());
            writer.WriteNumber("width", atlas.Image.Width);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string CreateDocumentation(
        AssetStudioDocument document,
        string fingerprint)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"# {document.Recipe.RecipeId}");
        builder.AppendLine();
        builder.AppendLine($"- Fingerprint: `{fingerprint}`");
        builder.AppendLine($"- Schema: `{document.Recipe.SchemaVersion}`");
        builder.AppendLine($"- Nodes: `{document.Recipe.Nodes.Length}`");
        builder.AppendLine(
            $"- Connections: `{document.Recipe.Connections.Length}`");
        builder.AppendLine();
        builder.AppendLine("## Nodes");
        foreach (var node in document.Recipe.Nodes.OrderBy(node => node.Id))
        {
            builder.Append("- `")
                .Append(node.Id)
                .Append("` ")
                .Append(node.TypeId)
                .Append(" v")
                .Append(node.Version)
                .AppendLine();
        }

        builder.AppendLine();
        builder.AppendLine("## Outputs");
        foreach (var output in document.Recipe.Outputs.OrderBy(
                     output => output.Id,
                     StringComparer.Ordinal))
        {
            builder.Append("- `")
                .Append(output.Id)
                .Append("` ")
                .Append(output.Name)
                .Append(" -> `")
                .Append(output.Source)
                .AppendLine("`");
        }

        return builder.ToString();
    }

    private static string SerializeBakeManifest(VisualBakeManifest manifest)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("contentSha256", manifest.ContentSha256);
            writer.WriteNumber("height", manifest.Height);
            writer.WriteStartObject("metadata");
            foreach (var item in manifest.Metadata)
            {
                writer.WriteString(item.Key, item.Value);
            }

            writer.WriteEndObject();
            writer.WriteString("recipeFingerprint", manifest.RecipeFingerprint);
            writer.WriteString("target", manifest.Target.ToString());
            writer.WriteNumber("width", manifest.Width);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static string SerializeClosure(CodeOnlyClosureReport report)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("isClosed", report.IsClosed);
            writer.WriteStartArray("violations");
            foreach (var violation in report.Violations)
            {
                writer.WriteStringValue(violation);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void ValidateArtifacts(
        IEnumerable<AssetStudioExportArtifact> artifacts,
        AssetStudioExportLimits limits,
        ICollection<AssetStudioDiagnostic> diagnostics)
    {
        try
        {
            limits.Validate();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            diagnostics.Add(
                Error(
                    "AST202",
                    $"Export limits are invalid: {exception.ParamName}."));
            return;
        }

        var artifactArray = artifacts.ToArray();
        if (artifactArray.Length > limits.MaxArtifacts)
        {
            diagnostics.Add(
                Error(
                    "AST203",
                    $"Export produced more than {limits.MaxArtifacts} artifacts."));
        }

        foreach (var artifact in artifactArray)
        {
            if (artifact.LogicalPath.Length > limits.MaxPathLength)
            {
                diagnostics.Add(
                    Error(
                        "AST204",
                        $"Artifact path '{artifact.LogicalPath}' exceeds the path limit."));
            }

            if (artifact.Length > limits.MaxArtifactBytes)
            {
                diagnostics.Add(
                    Error(
                        "AST205",
                        $"Artifact '{artifact.LogicalPath}' exceeds the byte limit."));
            }
        }
    }

    private static void AddTextArtifact(
        ICollection<AssetStudioExportArtifact> artifacts,
        string path,
        string contentType,
        string content) =>
        AddArtifact(
            artifacts,
            path,
            contentType,
            Encoding.UTF8.GetBytes(content));

    private static void AddArtifact(
        ICollection<AssetStudioExportArtifact> artifacts,
        string path,
        string contentType,
        IEnumerable<byte> content) =>
        artifacts.Add(
            new AssetStudioExportArtifact(
                path,
                contentType,
                content.ToArray()));

    private static string SafeAssetName(string assetId)
    {
        var chars = assetId
            .Where(character =>
                char.IsLetterOrDigit(character) ||
                character is '-' or '_' or '.')
            .ToArray();
        return chars.Length == 0 ? "asset" : new string(chars);
    }

    private static AssetStudioDiagnostic Error(string code, string message) =>
        new(AssetStudioDiagnosticSeverity.Error, code, message);
}
