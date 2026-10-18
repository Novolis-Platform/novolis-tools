using Novolis.Visual.Assets;
using Novolis.Visual.Definitions;
using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Host-neutral persistence facade for reusable material and asset modules.
/// </summary>
public static class AssetStudioModulePersistence
{
    /// <summary>Serializes the complete current document context.</summary>
    public static string SerializeDocument(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document.DocumentKind switch
        {
            AssetStudioDocumentKind.Material when document.Material is { } material =>
                SerializeMaterial(material),
            AssetStudioDocumentKind.Asset when document.Asset is { } asset =>
                SerializeAsset(asset),
            _ => AssetStudioDocumentPersistence.ToCanonicalJson(document),
        };
    }

    /// <summary>Restores a material, asset, or plain recipe document.</summary>
    public static AssetStudioDocument DeserializeDocument(
        string json,
        Guid? documentId = null,
        VisualNodeRegistry? registry = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("materialId", out _))
        {
            return AssetStudioDocument.CreateMaterial(
                DeserializeMaterial(json, registry),
                documentId);
        }

        if (root.TryGetProperty("assetId", out _))
        {
            return AssetStudioDocument.CreateAsset(
                DeserializeAsset(json, registry),
                documentId);
        }

        return AssetStudioDocumentPersistence.FromJson(
            json,
            documentId,
            registry);
    }

    public static AssetStudioModuleManifest Describe(MaterialGraph material) =>
        new(
            AssetStudioDocumentKind.Material,
            material.MaterialId,
            material.ModuleVersion,
            material.Fingerprint,
            material.Dependencies.Select(dependency => dependency.Id));

    public static AssetStudioModuleManifest Describe(AssetRecipe asset) =>
        new(
            AssetStudioDocumentKind.Asset,
            asset.AssetId,
            asset.ModuleVersion,
            asset.Fingerprint,
            asset.Dependencies.Select(dependency => dependency.Id));

    public static string SerializeMaterial(MaterialGraph material) =>
        MaterialGraphSerializer.Serialize(material);

    public static MaterialGraph DeserializeMaterial(
        string json,
        VisualNodeRegistry? registry = null) =>
        MaterialGraphSerializer.Deserialize(json, registry);

    public static string SerializeMaterialInstance(MaterialInstance instance) =>
        MaterialGraphSerializer.SerializeInstance(instance);

    public static MaterialInstance DeserializeMaterialInstance(
        string json,
        MaterialGraph parent) =>
        MaterialGraphSerializer.DeserializeInstance(json, parent);

    public static string SerializeAsset(AssetRecipe asset) =>
        AssetRecipeSerializer.Serialize(asset);

    public static AssetRecipe DeserializeAsset(
        string json,
        VisualNodeRegistry? registry = null) =>
        AssetRecipeSerializer.Deserialize(json, registry);

    public static string SerializeAssetInstance(AssetInstance instance) =>
        AssetRecipeSerializer.SerializeInstance(instance);

    public static AssetInstance DeserializeAssetInstance(
        string json,
        AssetRecipe parent) =>
        AssetRecipeSerializer.DeserializeInstance(json, parent);
}
