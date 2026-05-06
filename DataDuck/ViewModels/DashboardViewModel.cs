using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataDuck.Models;
using DataDuck.Services;

namespace DataDuck.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private const string StorageKey = "dataduck.dashboard.v1";

    private readonly ILocalStore? _store;
    private readonly IDuckDbService? _duckDb;
    private bool _suppressSave;

    public ObservableCollection<DashboardTile> Tiles { get; } = new();
    public ObservableCollection<TileResultViewModel> TileResults { get; } = new();

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string? _statusMessage;

    public bool HasTiles => Tiles.Count > 0;

    public DashboardViewModel(ILocalStore? store = null, IDuckDbService? duckDb = null)
    {
        _store = store;
        _duckDb = duckDb;

        Tiles.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasTiles));
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

    // Design-time only.
    public DashboardViewModel() : this(null, null) { }

    [RelayCommand]
    public void Pin(DashboardTile tile)
    {
        if (tile is null) return;
        Tiles.Insert(0, tile);
        _ = RefreshOneAsync(tile);
    }

    [RelayCommand]
    public void Remove(DashboardTile tile)
    {
        if (tile is null) return;
        Tiles.Remove(tile);
        var existing = TileResults.FirstOrDefault(t => t.TileId == tile.Id);
        if (existing != null) TileResults.Remove(existing);
    }

    [RelayCommand]
    public async Task RefreshAllAsync()
    {
        if (IsRefreshing) return;
        IsRefreshing = true;
        StatusMessage = null;
        try
        {
            // Snapshot to avoid concurrent-mutation issues if a tile is added mid-refresh.
            var snapshot = Tiles.ToList();
            foreach (var tile in snapshot)
            {
                await RefreshOneAsync(tile);
            }
            StatusMessage = $"Refreshed {snapshot.Count} tile{(snapshot.Count == 1 ? "" : "s")} at {DateTimeOffset.Now:HH:mm:ss}.";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private async Task RefreshOneAsync(DashboardTile tile)
    {
        var existing = TileResults.FirstOrDefault(t => t.TileId == tile.Id);
        var vm = existing ?? new TileResultViewModel(tile);

        if (existing is null)
        {
            // Mirror the tile's position in `Tiles` so the visual order matches insertion order.
            var idx = Tiles.IndexOf(tile);
            if (idx < 0 || idx > TileResults.Count) TileResults.Add(vm);
            else TileResults.Insert(idx, vm);
        }

        vm.Title = tile.Title;
        vm.IsLoading = true;
        vm.ErrorMessage = null;

        if (_duckDb is null)
        {
            vm.IsLoading = false;
            vm.ErrorMessage = "DuckDB service not available.";
            vm.Detect(null);
            return;
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _duckDb.QueryAsync(tile.Sql);
            sw.Stop();
            vm.ElapsedMs = result.ElapsedMs == 0 ? sw.ElapsedMilliseconds : result.ElapsedMs;
            vm.LastRefreshed = DateTimeOffset.Now;
            vm.Detect(result);
        }
        catch (Exception ex)
        {
            sw.Stop();
            vm.ElapsedMs = sw.ElapsedMilliseconds;
            vm.LastRefreshed = DateTimeOffset.Now;
            vm.ErrorMessage = ex.Message;
            vm.Detect(null);
        }
        finally
        {
            vm.IsLoading = false;
        }
    }

    private void Load()
    {
        if (_store is null) return;
        var json = _store.Get(StorageKey);
        if (string.IsNullOrWhiteSpace(json)) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<List<DashboardTile>>(json);
            if (loaded is null) return;

            _suppressSave = true;
            try
            {
                foreach (var tile in loaded) Tiles.Add(tile);
            }
            finally
            {
                _suppressSave = false;
            }

            // Kick off an initial refresh of every tile so the dashboard
            // isn't blank when the tab is first viewed.
            if (_duckDb is not null && Tiles.Count > 0)
            {
                _ = RefreshAllAsync();
            }
        }
        catch
        {
            // Corrupt dashboard — drop it silently.
        }
    }

    private void Save()
    {
        if (_store is null) return;
        try
        {
            _store.Set(StorageKey, JsonSerializer.Serialize(Tiles));
        }
        catch
        {
            // Quota or transient error — non-fatal.
        }
    }

    private void SeedDummyData()
    {
        var now = DateTimeOffset.Now;
        var tile1 = new DashboardTile(
            Guid.NewGuid(),
            "Total revenue",
            "SELECT SUM(amount) AS revenue FROM sales",
            now.AddMinutes(-12));
        var tile2 = new DashboardTile(
            Guid.NewGuid(),
            "Revenue by region",
            "SELECT region, SUM(amount) AS total FROM sales GROUP BY region ORDER BY total DESC",
            now.AddMinutes(-6));

        Tiles.Add(tile1);
        Tiles.Add(tile2);

        var r1 = new TileResultViewModel(tile1)
        {
            Title = tile1.Title,
            ElapsedMs = 18,
            LastRefreshed = now,
        };
        r1.Detect(new QueryResult(
            new[] { "revenue" },
            new List<object?[]> { new object?[] { 124_350.0 } },
            18));
        TileResults.Add(r1);

        var r2 = new TileResultViewModel(tile2)
        {
            Title = tile2.Title,
            ElapsedMs = 42,
            LastRefreshed = now,
        };
        r2.Detect(new QueryResult(
            new[] { "region", "total" },
            new List<object?[]>
            {
                new object?[] { "NA", 58_300.0 },
                new object?[] { "EU", 41_750.0 },
                new object?[] { "APAC", 18_120.0 },
                new object?[] { "LATAM", 6_180.0 },
            },
            42));
        TileResults.Add(r2);
    }
}

/// <summary>
/// Tracks a tile's last refresh outcome — title, elapsed, error,
/// detected chart shape (big-number or horizontal bar list).
/// One per <see cref="DashboardTile"/>; lives only in memory.
/// </summary>
public partial class TileResultViewModel : ViewModelBase
{
    public Guid TileId { get; }
    public DashboardTile Tile { get; }

    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private long _elapsedMs;
    [ObservableProperty] private DateTimeOffset? _lastRefreshed;

    [ObservableProperty] private ChartKind _kind = ChartKind.None;
    [ObservableProperty] private string? _bigNumberLabel;
    [ObservableProperty] private string? _bigNumberValue;
    [ObservableProperty] private string? _categoryColumn;
    [ObservableProperty] private string? _valueColumn;

    public ObservableCollection<BarPoint> BarPoints { get; } = new();

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool IsBigNumber => Kind == ChartKind.BigNumber;
    public bool IsBarChart => Kind == ChartKind.BarHorizontal;
    public bool IsEmpty => Kind == ChartKind.None && !HasError;

    public TileResultViewModel(DashboardTile tile)
    {
        Tile = tile;
        TileId = tile.Id;
        Title = tile.Title;
    }

    // Design-time only.
    public TileResultViewModel() : this(new DashboardTile(Guid.Empty, string.Empty, string.Empty, DateTimeOffset.Now)) { }

    partial void OnKindChanged(ChartKind value)
    {
        OnPropertyChanged(nameof(IsBigNumber));
        OnPropertyChanged(nameof(IsBarChart));
        OnPropertyChanged(nameof(IsEmpty));
    }

    partial void OnErrorMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Inspect a query result and pick a chart shape (big number or horizontal bar list).
    /// Mirrors a subset of <c>ChartViewModel</c>'s detection — only the two shapes the
    /// tile card supports today.
    /// </summary>
    public void Detect(QueryResult? result)
    {
        BarPoints.Clear();
        BigNumberLabel = null;
        BigNumberValue = null;
        CategoryColumn = null;
        ValueColumn = null;

        if (result is null)
        {
            Kind = ChartKind.None;
            return;
        }

        var cols = result.ColumnNames;
        var rows = result.Rows;

        if (cols.Count == 0 || rows.Count == 0)
        {
            Kind = ChartKind.None;
            return;
        }

        // 1) Single value: 1 col × 1 numeric row → big number.
        if (cols.Count == 1 && rows.Count == 1 && TryAsDouble(rows[0][0], out var single))
        {
            BigNumberLabel = cols[0];
            BigNumberValue = FormatNumber(single);
            Kind = ChartKind.BigNumber;
            return;
        }

        // 2) Bar chart: at least 2 cols, find first categorical + first numeric.
        var (catIdx, valIdx) = FindCategoricalAndNumeric(cols, rows);
        if (catIdx >= 0 && valIdx >= 0)
        {
            var staged = new List<(string label, double value)>();
            double max = 0;
            foreach (var row in rows.Take(8))
            {
                var label = row[catIdx]?.ToString() ?? "(null)";
                if (!TryAsDouble(row[valIdx], out var v)) continue;
                if (Math.Abs(v) > max) max = Math.Abs(v);
                staged.Add((label, v));
            }

            if (staged.Count >= 2 && max > 0)
            {
                CategoryColumn = cols[catIdx];
                ValueColumn = cols[valIdx];
                foreach (var (label, value) in staged)
                {
                    BarPoints.Add(new BarPoint
                    {
                        Label = label,
                        Value = Math.Abs(value),
                        Max = max,
                        Display = FormatNumber(value),
                    });
                }
                Kind = ChartKind.BarHorizontal;
                return;
            }
        }

        Kind = ChartKind.None;
    }

    private static (int catIdx, int valIdx) FindCategoricalAndNumeric(
        IReadOnlyList<string> cols, IReadOnlyList<object?[]> rows)
    {
        int catIdx = -1, valIdx = -1;
        for (var c = 0; c < cols.Count; c++)
        {
            var numericHits = 0;
            var nonNumericHits = 0;
            foreach (var row in rows.Take(5))
            {
                var val = row[c];
                if (val is null) continue;
                if (TryAsDouble(val, out _)) numericHits++;
                else nonNumericHits++;
            }
            var isNumeric = numericHits > 0 && numericHits >= nonNumericHits;
            if (isNumeric && valIdx < 0) valIdx = c;
            else if (!isNumeric && catIdx < 0) catIdx = c;
            if (catIdx >= 0 && valIdx >= 0) break;
        }
        return (catIdx, valIdx);
    }

    private static bool TryAsDouble(object? value, out double result)
    {
        switch (value)
        {
            case null: result = 0; return false;
            case double d: result = d; return true;
            case float f: result = f; return true;
            case long l: result = l; return true;
            case int i: result = i; return true;
            case short s: result = s; return true;
            case decimal m: result = (double)m; return true;
            case string str when double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var p):
                result = p; return true;
            default:
                result = 0;
                return false;
        }
    }

    private static string FormatNumber(double v)
    {
        var abs = Math.Abs(v);
        if (abs >= 1_000_000_000) return (v / 1_000_000_000d).ToString("0.##", CultureInfo.InvariantCulture) + "B";
        if (abs >= 1_000_000) return (v / 1_000_000d).ToString("0.##", CultureInfo.InvariantCulture) + "M";
        if (abs >= 1_000) return v.ToString("N0", CultureInfo.InvariantCulture);
        if (abs % 1 == 0) return v.ToString("N0", CultureInfo.InvariantCulture);
        return v.ToString("N2", CultureInfo.InvariantCulture);
    }
}
