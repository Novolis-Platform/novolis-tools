using Novolis.Visual.Assets;
using Novolis.Visual.Compile;
using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Immutable Asset Studio state for an assembled asset and its child instance.
/// </summary>
public sealed record AssetStudioAssetSession
{
    public AssetStudioAssetSession(
        AssetRecipe asset,
        AssetInstance? instance = null,
        long revision = 0)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        Asset = asset;
        Instance = instance ?? new AssetInstance(
            asset.AssetId + ".instance",
            asset);
        if (!string.Equals(
                Instance.Parent.AssetId,
                asset.AssetId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The asset instance belongs to another recipe.",
                nameof(instance));
        }

        Revision = revision;
    }

    public AssetRecipe Asset { get; }

    public AssetInstance Instance { get; }

    public long Revision { get; }

    public string Fingerprint =>
        VisualRecipeSerializer.SemanticFingerprint(ResolvedRecipe);

    public VisualRecipe ResolvedRecipe =>
        AssetRecipeResolver.Resolve(Asset, Instance);

    public AssetStudioAssetSession SetOverride(
        string parameterId,
        VisualValue value) =>
        new(
            Asset,
            Instance.WithOverride(parameterId, value),
            Revision + 1);

    public AssetStudioAssetSession ResetOverride(string parameterId) =>
        new(
            Asset,
            Instance.ResetOverride(parameterId),
            Revision + 1);

    public AssetStudioAssetSession ResetAll() =>
        new(Asset, Instance.ResetAll(), Revision + 1);

    public AssetStudioAssetSession Apply(IAssetStudioAssetCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        return command.Apply(this);
    }

    public VisualIR Compile(VisualCompileOptions? options = null) =>
        VisualMaterialCompiler.CompileAsset(Asset, Instance, options);
}
