using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia.Controls;
using DataDuck.Models;
using DataDuck.Services;

namespace DataDuck.ViewModels;

public partial class QueryHistoryViewModel : ViewModelBase
{
    private const string StorageKey = "dataduck.history.v1";
    private const int MaxItems = 50;

    private readonly ILocalStore? _store;
    private bool _suppressSave;

    public ObservableCollection<SavedQuery> Items { get; } = new();

    public bool HasItems => Items.Count > 0;

    /// <summary>Raised when the user clicks a history row.</summary>
    public event Action<SavedQuery>? QuerySelected;

    public QueryHistoryViewModel(ILocalStore? store = null)
    {
        _store = store;

        Items.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasItems));
            if (!_suppressSave) Save();
        };

        if (Design.IsDesignMode)
        {
            SeedDummyData();
        }
        else
        {
            Load();
        }
    }

    public void Add(SavedQuery query)
    {
        Items.Insert(0, query);
        while (Items.Count > MaxItems) Items.RemoveAt(Items.Count - 1);
    }

    public void Select(SavedQuery query) => QuerySelected?.Invoke(query);

    private void Load()
    {
        if (_store is null) return;
        var json = _store.Get(StorageKey);
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize(json, DataDuckJsonContext.Default.ListSavedQuery);
            if (loaded is null) return;

            _suppressSave = true;
            try
            {
                foreach (var item in loaded) Items.Add(item);
            }
            finally
            {
                _suppressSave = false;
            }
        }
        catch
        {
            // Corrupt history — drop it silently.
        }
    }

    private void Save()
    {
        if (_store is null) return;
        try
        {
            var snapshot = new List<SavedQuery>(Items);
            _store.Set(StorageKey, JsonSerializer.Serialize(snapshot, DataDuckJsonContext.Default.ListSavedQuery));
        }
        catch
        {
            // Quota or transient error — non-fatal.
        }
    }

    private void SeedDummyData()
    {
        var now = DateTimeOffset.Now;
        Items.Add(new SavedQuery(
            "SELECT region, SUM(amount) AS total FROM sales_2026_q1 GROUP BY region",
            now.AddMinutes(-2), 142));
        Items.Add(new SavedQuery(
            "SELECT * FROM customers WHERE country = 'US' LIMIT 100",
            now.AddMinutes(-7), 38));
        Items.Add(new SavedQuery(
            "SELECT COUNT(*) FROM sales_2026_q1",
            now.AddMinutes(-12), 18));
    }
}
