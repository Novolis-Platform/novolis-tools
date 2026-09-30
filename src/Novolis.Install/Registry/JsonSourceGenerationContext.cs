using System.Text.Json;
using System.Text.Json.Serialization;

namespace Novolis.Install.Registry;

[JsonSerializable(typeof(RegistryIndex))]
internal partial class JsonSourceGenerationContext : JsonSerializerContext;
