using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using DataDuck.Models;

namespace DataDuck.ViewModels;

public partial class ChartViewModel : ViewModelBase
{
    private readonly ResultsViewModel _results;

    [ObservableProperty] private ChartKind _kind = ChartKind.None;
    [ObservableProperty] private string? _categoryColumn;
    [ObservableProperty] private string? _valueColumn;
    [ObservableProperty] private string? _bigNumberValue;
    [ObservableProperty] private string? _bigNumberLabel;

    public ObservableCollection<BarPoint> BarPoints { get; } = new();

    public bool HasChart => Kind != ChartKind.None;
    public bool IsBigNumber => Kind == ChartKind.BigNumber;
    public bool IsBarChart => Kind == ChartKind.BarHorizontal;
    public bool IsEmpty => Kind == ChartKind.None;

    public ChartViewModel(ResultsViewModel results)
    {
        _results = results;
        results.ColumnsChanged += _ => Detect();
    }

    public ChartViewModel() : this(new ResultsViewModel()) { }

    partial void OnKindChanged(ChartKind value)
    {
        OnPropertyChanged(nameof(HasChart));
        OnPropertyChanged(nameof(IsBigNumber));
        OnPropertyChanged(nameof(IsBarChart));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void Detect()
    {
        BarPoints.Clear();
        BigNumberValue = null;
        BigNumberLabel = null;
        CategoryColumn = null;
        ValueColumn = null;

        var cols = _results.ColumnNames;
        var rows = _results.Rows;

        if (cols.Count == 0 || rows.Count == 0)
        {
            Kind = ChartKind.None;
            return;
        }

        // 1) Single value: 1 col × 1 numeric row → big number tile
        if (cols.Count == 1 && rows.Count == 1 && TryAsDouble(rows[0][0], out var single))
        {
            BigNumberLabel = cols[0];
            BigNumberValue = FormatNumber(single);
            Kind = ChartKind.BigNumber;
            return;
        }

        // 2) Bar chart: at least 2 cols, find first categorical + first numeric
        var (catIdx, valIdx) = FindCategoricalAndNumeric(cols, rows);
        if (catIdx >= 0 && valIdx >= 0)
        {
            var staged = new List<(string label, double value)>();
            double max = 0;
            foreach (var row in rows.Take(20))
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

        // Sample up to 5 rows per column to decide its type.
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
