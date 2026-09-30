using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Install.Registry;

/// <summary>
/// Reference to a single package manifest in the registry index.
/// </summary>
public sealed class RegistryPackageRef
{
    /// <summary>
    /// Package identifier (NuGet-style id).
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    /// <summary>
    /// Relative or absolute path/URL to the package manifest document.
    /// </summary>
    [JsonPropertyName("manifest")]
    public string Manifest { get; init; } = "";
}
