using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Novolis.AssetStudio.Authoring;

namespace Novolis.AssetStudio.Export;

/// <summary>
/// Deterministic report describing validation, closure, diagnostics, and output
/// counts for one export.
/// </summary>
public sealed record AssetStudioBuildReport
{
    /// <summary>
    /// Creates a build report.
    /// </summary>
    public AssetStudioBuildReport(
        AssetStudioExportProfile profile,
        string recipeFingerprint,
        bool success,
        IEnumerable<AssetStudioDiagnostic>? diagnostics,
        bool codeOnlyClosed,
        IEnumerable<string>? closureViolations,
        int artifactCount)
    {
        if (string.IsNullOrWhiteSpace(recipeFingerprint))
        {
            throw new ArgumentException(
                "A recipe fingerprint is required.",
                nameof(recipeFingerprint));
        }

        if (artifactCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(artifactCount));
        }

        Profile = profile;
        RecipeFingerprint = recipeFingerprint;
        Success = success;
        Diagnostics = new AssetStudioDiagnostics(diagnostics).Items;
        CodeOnlyClosed = codeOnlyClosed;
        ClosureViolations = (closureViolations ?? [])
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
        ArtifactCount = artifactCount;
    }

    /// <summary>
    /// Export profile.
    /// </summary>
    public AssetStudioExportProfile Profile { get; }

    /// <summary>
    /// Semantic recipe fingerprint.
    /// </summary>
    public string RecipeFingerprint { get; }

    /// <summary>
    /// Indicates whether export validation succeeded.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Ordered export diagnostics.
    /// </summary>
    public ImmutableArray<AssetStudioDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Indicates whether generated code passed the delegated closure verifier.
    /// </summary>
    public bool CodeOnlyClosed { get; }

    /// <summary>
    /// Delegated closure violations.
    /// </summary>
    public ImmutableArray<string> ClosureViolations { get; }

    /// <summary>
    /// Number of primary artifacts produced before report emission.
    /// </summary>
    public int ArtifactCount { get; }

    /// <summary>
    /// Serializes this report deterministically.
    /// </summary>
    public string ToCanonicalJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("artifactCount", ArtifactCount);
            writer.WriteStartArray("closureViolations");
            foreach (var violation in ClosureViolations)
            {
                writer.WriteStringValue(violation);
            }

            writer.WriteEndArray();
            writer.WriteBoolean("codeOnlyClosed", CodeOnlyClosed);
            writer.WriteStartArray("diagnostics");
            foreach (var diagnostic in Diagnostics)
            {
                writer.WriteStartObject();
                writer.WriteString("code", diagnostic.Code);
                writer.WriteString("message", diagnostic.Message);
                writer.WriteString("severity", diagnostic.Severity.ToString());
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteString("profile", Profile.ToString());
            writer.WriteString("recipeFingerprint", RecipeFingerprint);
            writer.WriteBoolean("success", Success);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
