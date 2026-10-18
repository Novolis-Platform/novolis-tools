namespace Novolis.AssetStudio.Authoring;

/// <summary>Resets one asset instance override.</summary>
public sealed record ResetAssetInstanceOverrideCommand(string ParameterId)
    : IAssetStudioCommand
{
    public string Description => "Reset asset instance override";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.AssetInstance is not { } instance)
        {
            return document;
        }

        return document.WithAssetInstance(
            instance.ResetOverride(ParameterId));
    }
}
