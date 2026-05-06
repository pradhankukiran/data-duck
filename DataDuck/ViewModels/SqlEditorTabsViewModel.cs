using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataDuck.Services;

namespace DataDuck.ViewModels;

/// <summary>
/// Owns a collection of <see cref="SqlEditorViewModel"/> instances (one per tab) and
/// exposes the currently active tab. Each tab has its own SqlText / EnglishQuestion /
/// IsRunning state but the tabs share the app-wide singletons
/// (ResultsViewModel, QueryHistoryViewModel, FileListViewModel) via the injected factory.
/// </summary>
public partial class SqlEditorTabsViewModel : ViewModelBase
{
    private const string StorageKey = "dataduck.editortabs.v1";

    private readonly ILocalStore? _store;
    private readonly Func<SqlEditorViewModel> _newEditor;
    private bool _suppressSave;

    public ObservableCollection<EditorTab> Tabs { get; } = new();

    [ObservableProperty]
    private EditorTab? _active;

    public bool CanCloseTab => Tabs.Count > 1;

    public SqlEditorTabsViewModel(Func<SqlEditorViewModel>? editorFactory = null, ILocalStore? store = null)
    {
        _store = store;
        _newEditor = editorFactory ?? (() => new SqlEditorViewModel());

        Tabs.CollectionChanged += OnTabsCollectionChanged;

        if (Design.IsDesignMode)
        {
            SeedDesignTimeTab();
        }
        else
        {
            Load();
        }

        if (Tabs.Count == 0) AddTab();
        Active ??= Tabs[0];
    }

    // Design-time only.
    public SqlEditorTabsViewModel() : this(null, null) { }

    [RelayCommand]
    public void SelectTab(EditorTab? tab)
    {
        if (tab is null) return;
        if (Tabs.Contains(tab)) Active = tab;
    }

    [RelayCommand]
    public void AddTab(string? sql = null)
    {
        var editor = _newEditor();
        if (sql is not null) editor.SqlText = sql;
        var tab = new EditorTab(Guid.NewGuid(), $"Query {Tabs.Count + 1}", editor);
        AttachEditor(tab);
        Tabs.Add(tab);
        Active = tab;
        Save();
    }

    [RelayCommand(CanExecute = nameof(CanCloseTab))]
    public void CloseTab(EditorTab tab)
    {
        if (tab is null) return;
        var index = Tabs.IndexOf(tab);
        if (index < 0) return;

        DetachEditor(tab);
        Tabs.Remove(tab);

        if (Tabs.Count == 0)
        {
            // Invariant: at least one tab.
            Active = null;
            AddTab();
            return;
        }

        if (ReferenceEquals(Active, tab) || Active is null)
        {
            Active = Tabs[Math.Min(index, Tabs.Count - 1)];
        }
        Save();
    }

    private void OnTabsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanCloseTab));
        CloseTabCommand.NotifyCanExecuteChanged();
    }

    private void AttachEditor(EditorTab tab)
    {
        tab.Editor.PropertyChanged += OnEditorPropertyChanged;
    }

    private void DetachEditor(EditorTab tab)
    {
        tab.Editor.PropertyChanged -= OnEditorPropertyChanged;
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Only persist non-transient state. SqlText is the only field we serialize today.
        if (e.PropertyName == nameof(SqlEditorViewModel.SqlText))
        {
            Save();
        }
    }

    private void Load()
    {
        if (_store is null) return;
        var json = _store.Get(StorageKey);
        if (string.IsNullOrWhiteSpace(json)) return;

        try
        {
            var loaded = JsonSerializer.Deserialize(json, DataDuckJsonContext.Default.ListPersistedTab);
            if (loaded is null || loaded.Count == 0) return;

            _suppressSave = true;
            try
            {
                foreach (var p in loaded)
                {
                    var editor = _newEditor();
                    if (!string.IsNullOrEmpty(p.Sql)) editor.SqlText = p.Sql;
                    var id = p.Id == Guid.Empty ? Guid.NewGuid() : p.Id;
                    var title = string.IsNullOrWhiteSpace(p.Title) ? $"Query {Tabs.Count + 1}" : p.Title;
                    var tab = new EditorTab(id, title, editor);
                    AttachEditor(tab);
                    Tabs.Add(tab);
                }
            }
            finally
            {
                _suppressSave = false;
            }
        }
        catch
        {
            // Corrupt persisted state — drop it silently.
        }
    }

    private void Save()
    {
        if (_suppressSave) return;
        if (_store is null) return;
        try
        {
            var snapshot = new List<PersistedTab>(Tabs.Count);
            foreach (var t in Tabs)
            {
                snapshot.Add(new PersistedTab
                {
                    Id = t.Id,
                    Title = t.Title,
                    Sql = t.Editor.SqlText,
                });
            }
            _store.Set(StorageKey, JsonSerializer.Serialize(snapshot, DataDuckJsonContext.Default.ListPersistedTab));
        }
        catch
        {
            // Quota or transient error — non-fatal.
        }
    }

    private void SeedDesignTimeTab()
    {
        var editor = _newEditor();
        editor.SqlText = "SELECT 1 AS hello;";
        var tab = new EditorTab(Guid.NewGuid(), "Query 1", editor);
        AttachEditor(tab);
        Tabs.Add(tab);
    }
}

internal sealed class PersistedTab
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Sql { get; set; } = string.Empty;
}

public sealed class EditorTab
{
    public Guid Id { get; }
    public string Title { get; set; }
    public SqlEditorViewModel Editor { get; }

    public EditorTab(Guid id, string title, SqlEditorViewModel editor)
    {
        Id = id;
        Title = title;
        Editor = editor;
    }
}
