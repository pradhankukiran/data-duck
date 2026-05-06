using System.Collections.Generic;
using System.Text.Json.Serialization;
using DataDuck.Models;
using DataDuck.ViewModels;

namespace DataDuck.Services;

/// <summary>
/// Source-generated JsonSerializer metadata for every type the shared DataDuck
/// core serializes/deserializes. Pass `DataDuckJsonContext.Default.<TypeName>`
/// to JsonSerializer.Serialize/Deserialize so the call is trim-safe (no
/// IL2026 warning) under aggressive trimming (e.g. the WASM browser head).
/// </summary>
[JsonSerializable(typeof(SavedQuery))]
[JsonSerializable(typeof(List<SavedQuery>))]
[JsonSerializable(typeof(DashboardTile))]
[JsonSerializable(typeof(List<DashboardTile>))]
[JsonSerializable(typeof(PersistedTab))]
[JsonSerializable(typeof(List<PersistedTab>))]
internal partial class DataDuckJsonContext : JsonSerializerContext
{
}
