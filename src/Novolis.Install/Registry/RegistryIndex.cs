using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Install.Registry;

/// <summary>
/// Top-level registry index listing available packages.
/// </summary>
public sealed class RegistryIndex
{
    /// <summary>
    /// Package entries advertised by the registry.
    /// </summary>
    [JsonPropertyName("packages")]
    public List<RegistryPackageRef> Packages { get; init; } = [];

    /// <summary>
    /// Downloads and deserializes a registry index from the given URI.
    /// </summary>
    /// <param name="indexUri">URI of the <c>index.json</c> document.</param>
    /// <param name="cancellationToken">Token used to cancel the HTTP request.</param>
    /// <returns>The parsed index, or an empty index when deserialization yields null.</returns>
    public static async Task<RegistryIndex> LoadAsync(Uri indexUri, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient();
        await using var stream = await client.GetStreamAsync(indexUri, cancellationToken);
        return await JsonSerializer.DeserializeAsync<RegistryIndex>(stream, JsonSourceGenerationContext.Default.RegistryIndex, cancellationToken)
            ?? new RegistryIndex();
    }
}

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

[JsonSerializable(typeof(RegistryIndex))]
internal partial class JsonSourceGenerationContext : JsonSerializerContext;
