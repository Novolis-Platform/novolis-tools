using System.Collections.Immutable;

namespace Novolis.AssetStudio.Authoring;

/// <summary>
/// Serializable identity for a reusable material or asset module.
/// </summary>
public sealed record AssetStudioModuleManifest
{
    public AssetStudioModuleManifest(
        AssetStudioDocumentKind kind,
        string moduleId,
        int moduleVersion,
        string fingerprint,
        IEnumerable<string>? dependencies = null)
    {
        if (kind is not
            (AssetStudioDocumentKind.Material or AssetStudioDocumentKind.Asset))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ModuleId = string.IsNullOrWhiteSpace(moduleId)
            ? throw new ArgumentException(
                "A module identifier is required.",
                nameof(moduleId))
            : moduleId.Trim();
        if (moduleVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(moduleVersion));
        }

        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            throw new ArgumentException(
                "A module fingerprint is required.",
                nameof(fingerprint));
        }

        Kind = kind;
        ModuleVersion = moduleVersion;
        Fingerprint = fingerprint.Trim();
        Dependencies = (dependencies ?? [])
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency))
            .Select(dependency => dependency.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToImmutableArray();
    }

    public AssetStudioDocumentKind Kind { get; }

    public string ModuleId { get; }

    public int ModuleVersion { get; }

    public string Fingerprint { get; }

    public ImmutableArray<string> Dependencies { get; }
}
