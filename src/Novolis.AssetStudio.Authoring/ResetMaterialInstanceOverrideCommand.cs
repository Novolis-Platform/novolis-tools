namespace Novolis.AssetStudio.Authoring;

/// <summary>Resets one material instance override.</summary>
public sealed record ResetMaterialInstanceOverrideCommand(string ParameterId)
    : IAssetStudioCommand
{
    public string Description => "Reset material instance override";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.MaterialInstance is not { } instance)
        {
            return document;
        }

        return document.WithMaterialInstance(
            instance.ResetOverride(ParameterId));
    }
}
