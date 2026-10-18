using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Novolis.AssetStudio.Export;

/// <summary>
/// Canonical deterministic manifest for an export result.
/// </summary>
public sealed record AssetStudioExportManifest
{
    /// <summary>
    /// Creates a sorted manifest.
    /// </summary>
    public AssetStudioExportManifest(
        string recipeFingerprint,
        AssetStudioExportProfile profile,
        IEnumerable<AssetStudioExportArtifact> artifacts)
    {
        if (string.IsNullOrWhiteSpace(recipeFingerprint))
        {
            throw new ArgumentException(
                "A recipe fingerprint is required.",
                nameof(recipeFingerprint));
        }

        ArgumentNullException.ThrowIfNull(artifacts);
        RecipeFingerprint = recipeFingerprint;
        Profile = profile;
        Entries = artifacts
            .Select(artifact => new AssetStudioExportManifestEntry(artifact))
            .OrderBy(entry => entry.LogicalPath, StringComparer.Ordinal)
            .ToImmutableArray();
    }

    /// <summary>
    /// Semantic recipe fingerprint represented by the manifest.
    /// </summary>
    public string RecipeFingerprint { get; }

    /// <summary>
    /// Profile represented by the manifest.
    /// </summary>
    public AssetStudioExportProfile Profile { get; }

    /// <summary>
    /// Sorted artifact entries.
    /// </summary>
    public ImmutableArray<AssetStudioExportManifestEntry> Entries { get; }

    /// <summary>
    /// Serializes the manifest using stable property and entry ordering.
    /// </summary>
    public string ToCanonicalJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions
                   {
                       Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Default,
                   }))
        {
            writer.WriteStartObject();
            writer.WriteString("profile", Profile.ToString());
            writer.WriteString("recipeFingerprint", RecipeFingerprint);
            writer.WritePropertyName("artifacts");
            writer.WriteStartArray();
            foreach (var entry in Entries)
            {
                writer.WriteStartObject();
                writer.WriteString("contentType", entry.ContentType);
                writer.WriteNumber("length", entry.Length);
                writer.WriteString("path", entry.LogicalPath);
                writer.WriteString("sha256", entry.Sha256);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// SHA-256 fingerprint of the canonical manifest JSON.
    /// </summary>
    public string Sha256 =>
        Convert.ToHexString(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(ToCanonicalJson())))
            .ToLowerInvariant();
}
