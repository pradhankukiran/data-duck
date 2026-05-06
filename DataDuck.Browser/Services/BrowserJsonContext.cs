using System.Text.Json.Serialization;

namespace DataDuck.Browser.Services;

/// <summary>
/// Source-generated JsonSerializer metadata for every type the browser head
/// serializes/deserializes (DuckDB-WASM register-file response, Groq chat
/// completion request body). Pass `BrowserJsonContext.Default.<TypeName>`
/// to JsonSerializer.Serialize/Deserialize so the call is trim-safe (no
/// IL2026 warning) under aggressive trimming.
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(RegisterResp))]
[JsonSerializable(typeof(ColInfo))]
[JsonSerializable(typeof(GroqChatRequest))]
[JsonSerializable(typeof(GroqChatMessage))]
[JsonSerializable(typeof(GroqResponseFormat))]
internal partial class BrowserJsonContext : JsonSerializerContext
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
