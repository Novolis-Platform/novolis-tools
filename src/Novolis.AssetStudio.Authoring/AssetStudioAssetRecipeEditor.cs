using Novolis.Visual.Assets;
using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

internal static class AssetStudioAssetRecipeEditor
{
    public static AssetRecipe AddComponent(
        AssetRecipe asset,
        AssetComponent component)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(component);
        if (asset.Components.Any(
                candidate => candidate.Id == component.Id))
        {
            throw new InvalidOperationException(
                $"Asset component '{component.Id}' already exists.");
        }

        return With(asset, components: asset.Components.Append(component));
    }

    public static AssetRecipe RemoveComponent(
        AssetRecipe asset,
        string componentId)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var component = asset.Components.FirstOrDefault(
            candidate => candidate.Id == componentId)
            ?? throw new InvalidOperationException(
                $"Asset component '{componentId}' does not exist.");
        _ = component;
        return With(
            asset,
            components: asset.Components.Where(
                candidate => candidate.Id != componentId));
    }

    public static AssetRecipe AddSlot(
        AssetRecipe asset,
        AssetMaterialSlot slot)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(slot);
        if (asset.MaterialSlots.Any(candidate => candidate.Id == slot.Id))
        {
            throw new InvalidOperationException(
                $"Material slot '{slot.Id}' already exists.");
        }

        return With(asset, materialSlots: asset.MaterialSlots.Append(slot));
    }

    public static AssetRecipe UpdateSlot(
        AssetRecipe asset,
        AssetMaterialSlot slot)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(slot);
        if (asset.FindSlot(slot.Id) is null)
        {
            throw new InvalidOperationException(
                $"Material slot '{slot.Id}' does not exist.");
        }

        return With(
            asset,
            materialSlots: asset.MaterialSlots.Select(
                candidate => candidate.Id == slot.Id ? slot : candidate));
    }

    public static AssetRecipe RemoveSlot(
        AssetRecipe asset,
        string slotId)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (asset.FindSlot(slotId) is null)
        {
            throw new InvalidOperationException(
                $"Material slot '{slotId}' does not exist.");
        }

        if (asset.Components.Any(
                component => component.MaterialSlotId == slotId))
        {
            throw new InvalidOperationException(
                $"Material slot '{slotId}' is still assigned to a component.");
        }

        return With(
            asset,
            materialSlots: asset.MaterialSlots.Where(
                slot => slot.Id != slotId));
    }

    public static AssetRecipe SetParameterDefault(
        AssetRecipe asset,
        string parameterId,
        VisualValue value)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentNullException.ThrowIfNull(value);
        var parameter = asset.FindParameter(parameterId)
            ?? throw new InvalidOperationException(
                $"Asset parameter '{parameterId}' does not exist.");
        if (!parameter.Contract.IsValueCompatibleWith(value.Contract))
        {
            throw new ArgumentException(
                $"Value for asset parameter '{parameterId}' is incompatible.",
                nameof(value));
        }

        var graph = asset.Graph;
        if (parameter.Target is { } target)
        {
            graph = AssetStudioRecipeEditor.SetNodeValue(
                graph,
                target.NodeId,
                target.ValueName,
                value);
        }

        var replacement = new AssetParameterDefinition(
            parameter.Id,
            parameter.Name,
            parameter.Contract,
            value,
            parameter.Editor,
            parameter.IsInstanceEditable,
            parameter.Target);
        return new AssetRecipe(
            asset.AssetId,
            asset.DisplayName,
            graph,
            asset.Components,
            asset.MaterialSlots,
            asset.Parameters.Select(
                candidate => candidate.Id == parameterId
                    ? replacement
                    : candidate),
            asset.Tags,
            asset.ModuleVersion,
            asset.Dependencies);
    }

    private static AssetRecipe With(
        AssetRecipe asset,
        IEnumerable<AssetComponent>? components = null,
        IEnumerable<AssetMaterialSlot>? materialSlots = null) =>
        new(
            asset.AssetId,
            asset.DisplayName,
            asset.Graph,
            components ?? asset.Components,
            materialSlots ?? asset.MaterialSlots,
            asset.Parameters,
            asset.Tags,
            asset.ModuleVersion,
            asset.Dependencies);
}
