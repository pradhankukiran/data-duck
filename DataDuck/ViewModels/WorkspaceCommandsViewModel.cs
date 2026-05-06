using DataDuck.Models;
using DataDuck.Services;

namespace DataDuck.ViewModels;

/// <summary>
/// Glue VM that builds a <see cref="WorkspaceSnapshot"/> from the current live
/// state (history, dashboards, editor tabs) and applies a snapshot back. Intended
/// to be wired by the parent view-model to a pair of "Export workspace" /
/// "Import workspace" buttons; the actual clipboard / file IO is owned by the
/// view layer (mirrors <see cref="ExportCommandsViewModel"/>).
/// </summary>
/// <remarks>
/// Settings.GroqApiKey is intentionally NOT included in the snapshot — exporting
/// secrets in a portable workspace file is a footgun, and the recipient is
/// expected to supply their own key.
/// </remarks>
public partial class WorkspaceCommandsViewModel : ViewModelBase
{
    private readonly QueryHistoryViewModel _history;
    private readonly DashboardViewModel _dashboard;
    private readonly SqlEditorTabsViewModel _tabs;

    public WorkspaceCommandsViewModel(
        QueryHistoryViewModel history,
        DashboardViewModel dashboard,
        SqlEditorTabsViewModel tabs)
    {
        _history = history;
        _dashboard = dashboard;
        _tabs = tabs;
    }

    // Design-time only.
    public WorkspaceCommandsViewModel() : this(
        new QueryHistoryViewModel(),
        new DashboardViewModel(),
        new SqlEditorTabsViewModel())
    {
    }

    /// <summary>Build a snapshot from the current live state.</summary>
    public WorkspaceSnapshot Snapshot()
    {
        var snap = new WorkspaceSnapshot();
        foreach (var q in _history.Items) snap.History.Add(q);
        foreach (var t in _dashboard.Tiles) snap.Dashboards.Add(t);
        foreach (var tab in _tabs.Tabs)
        {
            snap.EditorTabs.Add(new WorkspaceEditorTab
            {
                Id = tab.Id,
                Title = tab.Title,
                Sql = tab.Editor.SqlText,
            });
        }
        return snap;
    }

    /// <summary>
    /// Apply a snapshot. Policy: <b>replace</b> history and dashboards (full
    /// overwrite of those collections), <b>append</b> editor tabs (don't
    /// destroy what the user already has open). The parent can layer richer
    /// "merge vs replace" semantics on top later.
    /// </summary>
    public void Apply(WorkspaceSnapshot snap)
    {
        // History — replace.
        _history.Items.Clear();
        foreach (var q in snap.History) _history.Items.Add(q);

        // Dashboards — replace.
        _dashboard.Tiles.Clear();
        foreach (var t in snap.Dashboards) _dashboard.Tiles.Add(t);

        // Editor tabs — append. Use AddTab so the SqlEditorTabsViewModel's
        // internal bookkeeping (Active selection, persistence, attached events)
        // stays consistent.
        foreach (var tab in snap.EditorTabs)
        {
            _tabs.AddTab(tab.Sql);
        }
    }

    /// <summary>Snapshot the current workspace and serialize to indented JSON.</summary>
    public string ExportJson() => WorkspaceExporter.ToJson(Snapshot());

    /// <summary>
    /// Try to parse and apply a workspace JSON. Returns <c>false</c> on parse
    /// failure (corrupt file) without throwing.
    /// </summary>
    public bool TryImportJson(string json)
    {
        var snap = WorkspaceExporter.FromJson(json);
        if (snap is null) return false;
        Apply(snap);
        return true;
    }
}
