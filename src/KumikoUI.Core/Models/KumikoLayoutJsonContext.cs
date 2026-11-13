using System.Text.Json.Serialization;

namespace KumikoUI.Core.Models;

/// <summary>
/// The System.Text.Json source-generated metadata for <see cref="KumikoLayoutState"/>: no reflection, so the JSON
/// helpers work under Native AOT and full trimming.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowTrailingCommas = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip)]
[JsonSerializable(typeof(KumikoLayoutState))]
internal sealed partial class KumikoLayoutJsonContext : JsonSerializerContext
{
}
