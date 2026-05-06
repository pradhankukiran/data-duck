using System.Text.Json;
using DataDuck.Models;

namespace DataDuck.Services;

/// <summary>
/// Pure-string serializer for <see cref="WorkspaceSnapshot"/>. The caller is
/// responsible for actually pushing the JSON to the clipboard or to a file —
/// this class has no IO and no UI dependencies (mirrors <see cref="ResultExporter"/>).
/// </summary>
public static class WorkspaceExporter
{
    /// <summary>Serialize a snapshot to pretty-printed indented JSON.</summary>
    public static string ToJson(WorkspaceSnapshot snapshot)
        => JsonSerializer.Serialize(snapshot, WorkspaceJsonContext.Default.WorkspaceSnapshot);

    /// <summary>
    /// Deserialize a snapshot from JSON. Returns <c>null</c> on any parse failure
    /// — corrupt files are surfaced by a <c>null</c> result rather than an
    /// exception, so callers can give a friendly error message.
    /// </summary>
    public static WorkspaceSnapshot? FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, WorkspaceJsonContext.Default.WorkspaceSnapshot);
        }
        catch
        {
            return null;
        }
    }
}
