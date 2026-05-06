using System;
using System.Collections.Generic;

namespace DataDuck.Models;

/// <summary>
/// Serializable snapshot of a DataDuck workspace — saved queries, pinned dashboard
/// tiles, and open editor tabs. Loaded data files are intentionally not included
/// (binary, recipient must re-upload). Settings such as <c>GroqApiKey</c> are also
/// omitted: secrets should not travel inside an exportable workspace file.
/// </summary>
public sealed class WorkspaceSnapshot
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.UtcNow;
    public string AppVersion { get; set; } = "DataDuck v0.3";
    public List<SavedQuery> History { get; set; } = new();
    public List<DashboardTile> Dashboards { get; set; } = new();
    public List<WorkspaceEditorTab> EditorTabs { get; set; } = new();
}

/// <summary>
/// Serializable view of a single open editor tab. Only id/title/sql is persisted;
/// transient flags (IsRunning, EnglishQuestion, etc.) are excluded by design.
/// </summary>
public sealed class WorkspaceEditorTab
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Sql { get; set; } = "";
}
