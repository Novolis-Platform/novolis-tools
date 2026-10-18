using Novolis.Visual.Definitions;
using Novolis.Visual.Assets;
using Novolis.Visual.Materials;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Immutable Asset Studio document containing revisioned semantic and view
/// snapshots.
/// </summary>
public sealed record AssetStudioDocument
{
    /// <summary>
    /// Creates a document from a semantic recipe.
    /// </summary>
    public AssetStudioDocument(
        VisualRecipe recipe,
        Guid? documentId = null,
        AssetStudioViewState? view = null,
        long revision = 0,
        AssetStudioDocumentKind documentKind = AssetStudioDocumentKind.Recipe,
        MaterialGraph? material = null,
        AssetRecipe? asset = null,
        MaterialInstance? materialInstance = null,
        AssetInstance? assetInstance = null)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (revision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(revision));
        }

        DocumentId = documentId.GetValueOrDefault(Guid.NewGuid());
        if (DocumentId == Guid.Empty)
        {
            throw new ArgumentException(
                "A document identifier is required.",
                nameof(documentId));
        }

        ValidateContext(
            recipe,
            documentKind,
            material,
            asset,
            materialInstance,
            assetInstance);
        Semantic = new AssetStudioSemanticState(recipe, revision);
        View = view ?? new AssetStudioViewState();
        Revision = revision;
        DocumentKind = documentKind;
        Material = material;
        Asset = asset;
        MaterialInstance = materialInstance;
        AssetInstance = assetInstance;
    }

    private AssetStudioDocument(
        Guid documentId,
        AssetStudioSemanticState semantic,
        AssetStudioViewState view,
        long revision,
        AssetStudioDocumentKind documentKind,
        MaterialGraph? material,
        AssetRecipe? asset,
        MaterialInstance? materialInstance,
        AssetInstance? assetInstance)
    {
        DocumentId = documentId;
        Semantic = semantic;
        View = view;
        Revision = revision;
        DocumentKind = documentKind;
        Material = material;
        Asset = asset;
        MaterialInstance = materialInstance;
        AssetInstance = assetInstance;
    }

    /// <summary>
    /// Creates a new document with a generated identifier.
    /// </summary>
    public static AssetStudioDocument Create(
        VisualRecipe recipe,
        Guid? documentId = null) =>
        new(recipe, documentId);

    /// <summary>
    /// Creates a document whose semantic context is a reusable material graph.
    /// </summary>
    public static AssetStudioDocument CreateMaterial(
        MaterialGraph material,
        Guid? documentId = null,
        MaterialInstance? instance = null) =>
        new(
            material?.Recipe
                ?? throw new ArgumentNullException(nameof(material)),
            documentId,
            documentKind: AssetStudioDocumentKind.Material,
            material: material,
            materialInstance: instance);

    /// <summary>
    /// Creates a document whose semantic context is a reusable asset recipe.
    /// </summary>
    public static AssetStudioDocument CreateAsset(
        AssetRecipe asset,
        Guid? documentId = null,
        AssetInstance? instance = null) =>
        new(
            asset?.Graph
                ?? throw new ArgumentNullException(nameof(asset)),
            documentId,
            documentKind: AssetStudioDocumentKind.Asset,
            asset: asset,
            assetInstance: instance);

    /// <summary>
    /// Stable identity for this document independent of semantic revisions.
    /// </summary>
    public Guid DocumentId { get; }

    /// <summary>
    /// Semantic kind of this document.
    /// </summary>
    public AssetStudioDocumentKind DocumentKind { get; }

    /// <summary>
    /// Material module context when <see cref="DocumentKind"/> is
    /// <see cref="AssetStudioDocumentKind.Material"/>.
    /// </summary>
    public MaterialGraph? Material { get; }

    /// <summary>
    /// Asset module context when <see cref="DocumentKind"/> is
    /// <see cref="AssetStudioDocumentKind.Asset"/>.
    /// </summary>
    public AssetRecipe? Asset { get; }

    /// <summary>
    /// Optional material child instance inheriting the current material
    /// module.
    /// </summary>
    public MaterialInstance? MaterialInstance { get; }

    /// <summary>
    /// Optional asset child instance inheriting the current asset module.
    /// </summary>
    public AssetInstance? AssetInstance { get; }

    /// <summary>
    /// Fingerprint including module identity and instance overrides.
    /// </summary>
    public string ContextFingerprint =>
        Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(
                        ContextFingerprintSource())))
            .ToLowerInvariant();

    /// <summary>
    /// The immutable semantic snapshot.
    /// </summary>
    public AssetStudioSemanticState Semantic { get; }

    /// <summary>
    /// The immutable editor view snapshot.
    /// </summary>
    public AssetStudioViewState View { get; }

    /// <summary>
    /// Total document revision. Semantic and view revisions are also available
    /// independently through <see cref="Semantic"/> and <see cref="View"/>.
    /// </summary>
    public long Revision { get; }

    /// <summary>
    /// Convenience access to the canonical recipe.
    /// </summary>
    public VisualRecipe Recipe => Semantic.Recipe;

    /// <summary>
    /// Convenience access to the semantic revision.
    /// </summary>
    public long SemanticRevision => Semantic.Revision;

    /// <summary>
    /// Convenience access to the view revision.
    /// </summary>
    public long ViewRevision => View.Revision;

    /// <summary>
    /// Returns a document with a new semantic recipe and a new semantic
    /// revision.
    /// </summary>
    public AssetStudioDocument WithRecipe(VisualRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (ReferenceEquals(recipe, Recipe) ||
            recipe.SemanticFingerprint() == Semantic.Fingerprint)
        {
            return this;
        }

        var material = Material is null
            ? null
            : new MaterialGraph(
                Material.MaterialId,
                Material.DisplayName,
                recipe,
                Material.Channels,
                Material.Parameters,
                Material.Layers,
                Material.Tags,
                Material.ModuleVersion,
                Material.Dependencies);
        var materialInstance = MaterialInstance is null || material is null
            ? null
            : new MaterialInstance(
                MaterialInstance.InstanceId,
                material,
                MaterialInstance.Overrides);
        var asset = Asset is null
            ? null
            : new AssetRecipe(
                Asset.AssetId,
                Asset.DisplayName,
                recipe,
                Asset.Components,
                Asset.MaterialSlots,
                Asset.Parameters,
                Asset.Tags,
                Asset.ModuleVersion,
                Asset.Dependencies);
        var assetInstance = AssetInstance is null || asset is null
            ? null
            : new AssetInstance(
                AssetInstance.InstanceId,
                asset,
                AssetInstance.Overrides);
        return new AssetStudioDocument(
            DocumentId,
            new AssetStudioSemanticState(recipe, Semantic.Revision + 1),
            View,
            Revision + 1,
            DocumentKind,
            material,
            asset,
            materialInstance,
            assetInstance);
    }

    /// <summary>
    /// Returns a document with a complete material context replacement.
    /// </summary>
    public AssetStudioDocument WithMaterial(MaterialGraph material)
    {
        ArgumentNullException.ThrowIfNull(material);
        if (DocumentKind != AssetStudioDocumentKind.Material)
        {
            throw new InvalidOperationException(
                "Only material documents can hold a material graph.");
        }

        var next = WithRecipe(material.Recipe);
        return new AssetStudioDocument(
            next.DocumentId,
            next.Semantic,
            next.View,
            next.Revision,
            AssetStudioDocumentKind.Material,
            material,
            asset: null,
            materialInstance: MaterialInstance is null
                ? null
                : new MaterialInstance(
                    MaterialInstance.InstanceId,
                    material,
                    MaterialInstance.Overrides),
            assetInstance: null);
    }

    /// <summary>
    /// Returns a document with a complete asset context replacement.
    /// </summary>
    public AssetStudioDocument WithAsset(AssetRecipe asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (DocumentKind != AssetStudioDocumentKind.Asset)
        {
            throw new InvalidOperationException(
                "Only asset documents can hold an asset recipe.");
        }

        var next = WithRecipe(asset.Graph);
        return new AssetStudioDocument(
            next.DocumentId,
            next.Semantic,
            next.View,
            next.Revision,
            AssetStudioDocumentKind.Asset,
            material: null,
            asset: asset,
            materialInstance: null,
            assetInstance: AssetInstance is null
                ? null
                : new AssetInstance(
                    AssetInstance.InstanceId,
                    asset,
                    AssetInstance.Overrides));
    }

    /// <summary>Returns a material document with a compatible child instance.</summary>
    public AssetStudioDocument WithMaterialInstance(
        MaterialInstance? instance)
    {
        if (DocumentKind != AssetStudioDocumentKind.Material ||
            Material is null)
        {
            throw new InvalidOperationException(
                "A material instance requires a material document.");
        }

        ValidateInstanceParent(instance, Material);
        return new AssetStudioDocument(
            DocumentId,
            new AssetStudioSemanticState(
                Semantic.Recipe,
                Semantic.Revision + 1),
            View,
            Revision + 1,
            DocumentKind,
            Material,
            asset: null,
            materialInstance: instance,
            assetInstance: null);
    }

    /// <summary>Returns an asset document with a compatible child instance.</summary>
    public AssetStudioDocument WithAssetInstance(AssetInstance? instance)
    {
        if (DocumentKind != AssetStudioDocumentKind.Asset ||
            Asset is null)
        {
            throw new InvalidOperationException(
                "An asset instance requires an asset document.");
        }

        ValidateInstanceParent(instance, Asset);
        return new AssetStudioDocument(
            DocumentId,
            new AssetStudioSemanticState(
                Semantic.Recipe,
                Semantic.Revision + 1),
            View,
            Revision + 1,
            DocumentKind,
            material: null,
            asset: Asset,
            materialInstance: null,
            assetInstance: instance);
    }

    /// <summary>
    /// Returns a document with new view state and a new view revision.
    /// </summary>
    public AssetStudioDocument WithView(AssetStudioViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view == View)
        {
            return this;
        }

        return new AssetStudioDocument(
            DocumentId,
            Semantic,
            new AssetStudioViewState(
                view.Editor,
                view.ActiveOutputId,
                view.PinnedNodes,
                View.Revision + 1),
            Revision + 1,
            DocumentKind,
            Material,
            Asset,
            MaterialInstance,
            AssetInstance);
    }

    /// <summary>
    /// Restores semantic and view content from an earlier snapshot while
    /// keeping this document's revision counters monotonic.
    /// </summary>
    public AssetStudioDocument RestoreContent(AssetStudioDocument snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.DocumentId != DocumentId)
        {
            throw new ArgumentException(
                "The snapshot belongs to another document.",
                nameof(snapshot));
        }

        var semanticChanged = Semantic.Fingerprint != snapshot.Semantic.Fingerprint;
        var viewChanged = View != snapshot.View;
        if (!semanticChanged && !viewChanged)
        {
            return this;
        }

        return new AssetStudioDocument(
            DocumentId,
            semanticChanged
                ? new AssetStudioSemanticState(
                    snapshot.Recipe,
                    Semantic.Revision + 1)
                : Semantic,
            viewChanged
                ? new AssetStudioViewState(
                    snapshot.View.Editor,
                    snapshot.View.ActiveOutputId,
                    snapshot.View.PinnedNodes,
                    View.Revision + 1)
                : View,
            Revision + 1,
            snapshot.DocumentKind,
            snapshot.Material,
            snapshot.Asset,
            snapshot.MaterialInstance,
            snapshot.AssetInstance);
    }

    private static void ValidateContext(
        VisualRecipe recipe,
        AssetStudioDocumentKind documentKind,
        MaterialGraph? material,
        AssetRecipe? asset,
        MaterialInstance? materialInstance,
        AssetInstance? assetInstance)
    {
        switch (documentKind)
        {
            case AssetStudioDocumentKind.Recipe
                when material is not null || asset is not null:
                throw new ArgumentException(
                    "A recipe document cannot carry a material or asset context.");
            case AssetStudioDocumentKind.Material
                when material is null ||
                     !ReferenceEquals(material.Recipe, recipe) ||
                     (materialInstance is not null &&
                      !string.Equals(
                          materialInstance.Parent.MaterialId,
                          material.MaterialId,
                          StringComparison.Ordinal)):
                throw new ArgumentException(
                    "A material document must carry the same recipe as its graph.",
                    nameof(material));
            case AssetStudioDocumentKind.Asset
                when asset is null ||
                     !ReferenceEquals(asset.Graph, recipe) ||
                     (assetInstance is not null &&
                      !string.Equals(
                          assetInstance.Parent.AssetId,
                          asset.AssetId,
                          StringComparison.Ordinal)):
                throw new ArgumentException(
                    "An asset document must carry the same recipe as its asset.",
                    nameof(asset));
            case AssetStudioDocumentKind.Recipe
                when materialInstance is not null || assetInstance is not null:
                throw new ArgumentException(
                    "A recipe document cannot carry a child instance.");
            case AssetStudioDocumentKind.Recipe:
            case AssetStudioDocumentKind.Material:
            case AssetStudioDocumentKind.Asset:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(documentKind));
        }
    }

    private static void ValidateInstanceParent(
        MaterialInstance? instance,
        MaterialGraph material)
    {
        if (instance is not null &&
            !string.Equals(
                instance.Parent.MaterialId,
                material.MaterialId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The material instance belongs to another module.",
                nameof(instance));
        }
    }

    private static void ValidateInstanceParent(
        AssetInstance? instance,
        AssetRecipe asset)
    {
        if (instance is not null &&
            !string.Equals(
                instance.Parent.AssetId,
                asset.AssetId,
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The asset instance belongs to another module.",
                nameof(instance));
        }
    }

    private string ContextFingerprintSource()
    {
        var module = Material?.Fingerprint ?? Asset?.Fingerprint ?? Semantic.Fingerprint;
        var overrides = MaterialInstance?.Overrides
            .Select(pair => pair.Key + "=" +
                            VisualRecipeSerializer.SerializeValue(pair.Value))
            .Concat(
                AssetInstance?.Overrides.Select(
                    pair => pair.Key + "=" +
                            VisualRecipeSerializer.SerializeValue(pair.Value))
                ?? [])
            ?? [];
        return string.Join(
            "|",
            DocumentKind,
            module,
            MaterialInstance?.InstanceId,
            AssetInstance?.InstanceId,
            string.Join(";", overrides.Order(StringComparer.Ordinal)));
    }
}
