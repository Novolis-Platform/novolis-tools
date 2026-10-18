using System.Collections.Immutable;
using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Builds a generic node inspector from the injected descriptor registry.
/// </summary>
public static class AssetStudioNodeInspector
{
    /// <summary>
    /// Returns descriptor fields and current values for one node.
    /// </summary>
    public static ImmutableArray<AssetStudioInspectorField> Create(
        VisualRecipe recipe,
        NodeId nodeId,
        VisualNodeRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(registry);
        var node = recipe.Nodes.FirstOrDefault(
            candidate => candidate.Id == nodeId)
            ?? throw new InvalidOperationException(
                $"Node '{nodeId}' does not exist.");
        if (!registry.TryGetDescriptor(node.TypeId, out var descriptor) ||
            descriptor is null)
        {
            return [];
        }

        return descriptor.Parameters
            .Select(
                parameter => new AssetStudioInspectorField(
                    parameter.Name,
                    parameter.Editor.DisplayName ?? parameter.Name,
                    parameter.Contract,
                    node.Values.TryGetValue(parameter.Name, out var value)
                        ? value
                        : parameter.DefaultValue,
                    parameter.Editor,
                    parameter.IsExposed,
                    parameter.Description))
            .ToImmutableArray();
    }
}
