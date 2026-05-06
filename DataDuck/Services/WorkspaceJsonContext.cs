using System.Text.Json.Serialization;
using DataDuck.Models;

namespace DataDuck.Services;

/// <summary>
/// Source-generated JsonSerializer metadata for the exportable
/// <see cref="WorkspaceSnapshot"/>. Separate context (vs <see cref="DataDuckJsonContext"/>)
/// so we can configure indented output for human-friendly .dataduck files
/// without affecting other call sites.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(WorkspaceSnapshot))]
[JsonSerializable(typeof(WorkspaceEditorTab))]
internal partial class WorkspaceJsonContext : JsonSerializerContext
{
}
