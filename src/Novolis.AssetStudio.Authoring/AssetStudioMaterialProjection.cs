using System.Collections.Immutable;
using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Generic material authoring projections. Channels, layers, and controls
/// remain views over one graph and never become a second semantic model.
/// </summary>
public static class AssetStudioMaterialProjection
{
    public static ImmutableArray<MaterialChannelBinding> Channels(
        AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Material?.Channels ?? [];
    }

    public static ImmutableArray<MaterialLayer> Layers(
        AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Material?.Layers ?? [];
    }

    public static ImmutableArray<MaterialParameterDefinition> Controls(
        AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.Material?.Parameters ?? [];
    }
}
