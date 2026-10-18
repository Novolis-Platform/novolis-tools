using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Authoring;

/// <summary>Changes one material channel projection.</summary>
public sealed record SetMaterialChannelCommand : IAssetStudioCommand
{
    public SetMaterialChannelCommand(MaterialChannelBinding binding)
    {
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
    }

    public MaterialChannelBinding Binding { get; }

    public string Description => "Set material channel";

    public AssetStudioDocument Apply(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Material is not { } material)
        {
            throw new InvalidOperationException(
                "Material channels require a material document.");
        }

        return document.WithMaterial(
            AssetStudioMaterialEditor.SetChannel(material, Binding));
    }
}
