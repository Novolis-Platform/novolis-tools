using System.Text;
using Novolis.Visual.Definitions;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Canonical semantic persistence for Asset Studio documents.
/// </summary>
public static class AssetStudioDocumentPersistence
{
    /// <summary>
    /// Serializes only the semantic recipe through
    /// <see cref="VisualRecipeSerializer"/>.
    /// </summary>
    public static string ToCanonicalJson(AssetStudioDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return VisualRecipeSerializer.ToCanonicalJson(document.Recipe);
    }

    /// <summary>
    /// Serializes only the semantic recipe as UTF-8 bytes.
    /// </summary>
    public static byte[] ToUtf8(AssetStudioDocument document) =>
        Encoding.UTF8.GetBytes(ToCanonicalJson(document));

    /// <summary>
    /// Restores a document with a fresh view state from canonical recipe JSON.
    /// </summary>
    public static AssetStudioDocument FromJson(
        string json,
        Guid? documentId = null,
        VisualNodeRegistry? registry = null,
        VisualSchemaMigrationRegistry? migrations = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        var recipe = VisualRecipeSerializer.Deserialize(
            json,
            registry,
            migrations);
        return AssetStudioDocument.Create(recipe, documentId);
    }

    /// <summary>
    /// Restores a document from UTF-8 canonical recipe bytes.
    /// </summary>
    public static AssetStudioDocument FromUtf8(
        ReadOnlyMemory<byte> utf8,
        Guid? documentId = null,
        VisualNodeRegistry? registry = null,
        VisualSchemaMigrationRegistry? migrations = null) =>
        FromJson(
            Encoding.UTF8.GetString(utf8.Span),
            documentId,
            registry,
            migrations);
}
