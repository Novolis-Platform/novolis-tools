namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Returns one exposed asset control to its parent/default value.
/// </summary>
public sealed record ResetAssetParameterCommand : IAssetStudioAssetCommand
{
    public ResetAssetParameterCommand(string parameterId)
    {
        if (string.IsNullOrWhiteSpace(parameterId))
        {
            throw new ArgumentException(
                "An asset parameter identifier is required.",
                nameof(parameterId));
        }

        ParameterId = parameterId.Trim();
    }

    public string ParameterId { get; }

    public string Description => "Reset asset parameter";

    public AssetStudioAssetSession Apply(
        AssetStudioAssetSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.ResetOverride(ParameterId);
    }
}
