using Novolis.Registry;
using Novolis.Registry.Primitives;

namespace Novolis.Install.Registry;

/// <summary>
/// Downloads the provider-neutral registry document used by the installer.
/// </summary>
public static class RegistryIndex
{
    /// <summary>
    /// Downloads and deserializes a registry index from the given URI.
    /// </summary>
    /// <param name="indexUri">URI of the <c>index.json</c> document.</param>
    /// <param name="cancellationToken">Token used to cancel the HTTP request.</param>
    /// <returns>The parsed registry document.</returns>
    public static async Task<RegistryDocument> LoadAsync(
        Uri indexUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(indexUri);

        using var client = new HttpClient();
        await using var stream = await client.GetStreamAsync(indexUri, cancellationToken);
        return await RegistryJson.DeserializeAsync(stream, cancellationToken)
            .ConfigureAwait(false);
    }
}
