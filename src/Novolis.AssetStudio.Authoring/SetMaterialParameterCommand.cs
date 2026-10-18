using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Changes one exposed material control on the active instance.
/// </summary>
public sealed record SetMaterialParameterCommand : IAssetStudioMaterialCommand
{
    public SetMaterialParameterCommand(
        string parameterId,
        VisualValue value)
    {
        if (string.IsNullOrWhiteSpace(parameterId))
        {
            throw new ArgumentException(
                "A material parameter identifier is required.",
                nameof(parameterId));
        }

        ArgumentNullException.ThrowIfNull(value);
        ParameterId = parameterId.Trim();
        Value = value;
    }

    public string ParameterId { get; }

    public VisualValue Value { get; }

    public string Description => "Set material parameter";

    public AssetStudioMaterialSession Apply(
        AssetStudioMaterialSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.SetOverride(ParameterId, Value);
    }
}
