using System.Text.Json;

namespace Novolis.Tools.Docs.Org;

/// <summary>Serializer settings for <see cref="OrgStatusSnapshot"/>.</summary>
public static class OrgStatusJson
{
    /// <summary>Camel-case indented JSON.</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
}
