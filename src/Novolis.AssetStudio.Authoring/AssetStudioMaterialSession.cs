using Novolis.Visual.Compile;
using Novolis.Visual.Definitions;
using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Immutable Asset Studio state for a material graph and its child instance.
/// </summary>
public sealed record AssetStudioMaterialSession
{
    public AssetStudioMaterialSession(
        MaterialGraph graph,
        MaterialInstance? instance = null,
        long revision = 0)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        Graph = graph;
        Instance = instance ?? new MaterialInstance(
            graph.MaterialId + ".instance",
            graph);
        if (!string.Equals(
                Instance.Parent.MaterialId,
                graph.MaterialId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The material instance belongs to another graph.",
                nameof(instance));
        }

        Revision = revision;
    }

    public MaterialGraph Graph { get; }

    public MaterialInstance Instance { get; }

    public long Revision { get; }

    public string Fingerprint =>
        VisualRecipeSerializer.SemanticFingerprint(ResolvedRecipe);

    public VisualRecipe ResolvedRecipe =>
        MaterialGraphResolver.Resolve(Graph, Instance);

    public AssetStudioMaterialSession SetOverride(
        string parameterId,
        VisualValue value) =>
        new(
            Graph,
            Instance.WithOverride(parameterId, value),
            Revision + 1);

    public AssetStudioMaterialSession ResetOverride(string parameterId) =>
        new(
            Graph,
            Instance.ResetOverride(parameterId),
            Revision + 1);

    public AssetStudioMaterialSession ResetAll() =>
        new(Graph, Instance.ResetAll(), Revision + 1);

    public AssetStudioMaterialSession Apply(IAssetStudioMaterialCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Apply(this);
    }

    public VisualIR Compile(VisualCompileOptions? options = null) =>
        VisualMaterialCompiler.CompileMaterial(Graph, Instance, options);
}
