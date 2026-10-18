using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Changes one exposed asset control on the active reusable instance.
/// </summary>
public sealed record SetAssetParameterCommand : IAssetStudioAssetCommand
{
    public SetAssetParameterCommand(
        string parameterId,
        VisualValue value)
    {
        if (string.IsNullOrWhiteSpace(parameterId))
        {
            throw new ArgumentException(
                "An asset parameter identifier is required.",
                nameof(parameterId));
        }

        ArgumentNullException.ThrowIfNull(value);
        ParameterId = parameterId.Trim();
        Value = value;
    }

    public string ParameterId { get; }

    public VisualValue Value { get; }

    public string Description => "Set asset parameter";

    public AssetStudioAssetSession Apply(
        AssetStudioAssetSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.SetOverride(ParameterId, Value);
    }
}
