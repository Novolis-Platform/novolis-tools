using Novolis.Visual.Definitions;
using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Authoring;

internal static class AssetStudioMaterialEditor
{
    public static MaterialGraph SetParameterDefault(
        MaterialGraph material,
        string parameterId,
        VisualValue value)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(value);
        var parameter = material.FindParameter(parameterId)
            ?? throw new InvalidOperationException(
                $"Material parameter '{parameterId}' does not exist.");
        if (!parameter.Contract.IsValueCompatibleWith(value.Contract))
        {
            throw new ArgumentException(
                $"Value for material parameter '{parameterId}' is incompatible.",
                nameof(value));
        }

        var recipe = material.Recipe;
        if (parameter.Target is { } target)
        {
            recipe = AssetStudioRecipeEditor.SetNodeValue(
                recipe,
                target.NodeId,
                target.ValueName,
                value);
        }

        var replacement = new MaterialParameterDefinition(
            parameter.Id,
            parameter.Name,
            parameter.Contract,
            value,
            parameter.Editor,
            parameter.IsAnimatable,
            parameter.IsInstanceEditable,
            parameter.Target);
        return new MaterialGraph(
            material.MaterialId,
            material.DisplayName,
            recipe,
            material.Channels,
            material.Parameters.Select(
                candidate => candidate.Id == parameterId
                    ? replacement
                    : candidate),
            material.Layers,
            material.Tags,
            material.ModuleVersion,
            material.Dependencies);
    }

    public static MaterialGraph SetChannel(
        MaterialGraph material,
        MaterialChannelBinding binding)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(binding);
        var channels = material.Channels
            .Where(candidate => candidate.Channel != binding.Channel)
            .Append(binding);
        return With(material, channels: channels);
    }

    public static MaterialGraph RemoveChannel(
        MaterialGraph material,
        MaterialChannel channel) =>
        With(
            material,
            channels: material.Channels.Where(
                candidate => candidate.Channel != channel));

    public static MaterialGraph SetLayer(
        MaterialGraph material,
        MaterialLayer layer)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(layer);
        if (!material.Layers.Any(candidate => candidate.Id == layer.Id))
        {
            throw new InvalidOperationException(
                $"Material layer '{layer.Id}' does not exist.");
        }

        return With(
            material,
            layers: material.Layers.Select(
                candidate => candidate.Id == layer.Id ? layer : candidate));
    }

    public static MaterialGraph AddLayer(
        MaterialGraph material,
        MaterialLayer layer)
    {
        ArgumentNullException.ThrowIfNull(material);
        ArgumentNullException.ThrowIfNull(layer);
        if (material.Layers.Any(candidate => candidate.Id == layer.Id))
        {
            throw new InvalidOperationException(
                $"Material layer '{layer.Id}' already exists.");
        }

        return With(material, layers: material.Layers.Append(layer));
    }

    public static MaterialGraph RemoveLayer(
        MaterialGraph material,
        string layerId)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (!material.Layers.Any(candidate => candidate.Id == layerId))
        {
            throw new InvalidOperationException(
                $"Material layer '{layerId}' does not exist.");
        }

        return With(
            material,
            layers: material.Layers.Where(
                candidate => candidate.Id != layerId));
    }

    private static MaterialGraph With(
        MaterialGraph material,
        IEnumerable<MaterialChannelBinding>? channels = null,
        IEnumerable<MaterialLayer>? layers = null) =>
        new(
            material.MaterialId,
            material.DisplayName,
            material.Recipe,
            channels ?? material.Channels,
            material.Parameters,
            layers ?? material.Layers,
            material.Tags,
            material.ModuleVersion,
            material.Dependencies);
}
