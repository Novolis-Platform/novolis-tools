namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Restores one material control to its parent value.
/// </summary>
public sealed record ResetMaterialParameterCommand(
    string ParameterId) : IAssetStudioMaterialCommand
{
    public string Description => "Reset material parameter";

    public AssetStudioMaterialSession Apply(
        AssetStudioMaterialSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (string.IsNullOrWhiteSpace(ParameterId))
        {
            throw new ArgumentException(
                "A material parameter identifier is required.",
                nameof(ParameterId));
        }

        return session.ResetOverride(ParameterId);
    }
}
