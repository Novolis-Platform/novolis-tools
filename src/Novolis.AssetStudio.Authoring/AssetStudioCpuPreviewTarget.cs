using System.Text;
using Novolis.Visual.Compile;
using Novolis.Visual.Definitions;
using Novolis.Visual.Evaluation;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Deterministic CPU reference target for host-neutral preview requests.
/// It returns evaluated output data; a UI may rasterize that data separately.
/// </summary>
public sealed class AssetStudioCpuPreviewTarget : IAssetStudioPreviewTarget
{
    private readonly VisualNodeRegistry _registry;

    public AssetStudioCpuPreviewTarget(VisualNodeRegistry? registry = null)
    {
        _registry = registry ?? VisualNodeRegistry.CreateDefault();
    }

    public string TargetId => "cpu";

    public AssetStudioPreviewTargetKind Kind =>
        AssetStudioPreviewTargetKind.Cpu;

    public ValueTask<AssetStudioPreviewResult> PreviewAsync(
        AssetStudioPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var compilation = request.Compilation ?? Compile(request.Document);
        var diagnostics = compilation.Diagnostics
            .Select(
                diagnostic => AssetStudioDiagnostic.From(
                    diagnostic,
                    request.Document.Revision))
            .ToList();
        if (!compilation.Success)
        {
            return ValueTask.FromResult(
                new AssetStudioPreviewResult(
                    TargetId,
                    request.Document.Revision,
                    success: false,
                    "application/json",
                    diagnostics: diagnostics,
                    semanticFingerprint: request.SemanticFingerprint,
                    kind: Kind));
        }

        var evaluation = VisualEvaluator.Evaluate(compilation);
        diagnostics.AddRange(
            evaluation.Diagnostics.Select(
                diagnostic => AssetStudioDiagnostic.From(
                    diagnostic,
                    request.Document.Revision)));
        var payload = Encoding.UTF8.GetBytes(
            string.Join(
                Environment.NewLine,
                evaluation.Outputs.Select(
                    output =>
                        $"{output.Key}={VisualRecipeSerializer.SerializeValue(output.Value)}")));
        return ValueTask.FromResult(
            new AssetStudioPreviewResult(
                TargetId,
                request.Document.Revision,
                evaluation.Success,
                "application/x-novolis-visual-values",
                payload,
                diagnostics,
                request.SemanticFingerprint,
                Kind));
    }

    private VisualIR Compile(AssetStudioDocument document)
    {
        var options = new VisualCompileOptions(
            registry: _registry,
            strict: true);
        return document.DocumentKind switch
        {
            AssetStudioDocumentKind.Material when document.Material is { } material =>
                VisualMaterialCompiler.CompileMaterial(
                    material,
                    document.MaterialInstance,
                    options),
            AssetStudioDocumentKind.Asset when document.Asset is { } asset =>
                VisualMaterialCompiler.CompileAsset(
                    asset,
                    document.AssetInstance,
                    options),
            _ => VisualCompiler.Compile(document.Recipe, options),
        };
    }
}
