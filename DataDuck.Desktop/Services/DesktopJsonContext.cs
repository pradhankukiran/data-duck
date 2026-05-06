using System.Text.Json.Serialization;

namespace DataDuck.Desktop.Services;

/// <summary>
/// Source-generated JsonSerializer metadata for every type the desktop head
/// serializes/deserializes (Groq chat completion request body). Pass
/// `DesktopJsonContext.Default.<TypeName>` to JsonSerializer.Serialize/Deserialize
/// so the call is trim-safe (no IL2026 warning) under aggressive trimming.
/// </summary>
[JsonSerializable(typeof(GroqChatRequest))]
[JsonSerializable(typeof(GroqChatMessage))]
[JsonSerializable(typeof(GroqResponseFormat))]
internal partial class DesktopJsonContext : JsonSerializerContext
{
}

internal sealed record GroqChatRequest(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] GroqChatMessage[] Messages,
    [property: JsonPropertyName("temperature")] double Temperature,
    [property: JsonPropertyName("response_format"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GroqResponseFormat? ResponseFormat);

internal sealed record GroqChatMessage(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content);

internal sealed record GroqResponseFormat(
    [property: JsonPropertyName("type")] string Type);
