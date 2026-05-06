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
    [ObservableProperty] private string? _lineXAxisMin;
    [ObservableProperty] private string? _lineXAxisMax;
    [ObservableProperty] private string? _lineYAxisMin;
    [ObservableProperty] private string? _lineYAxisMax;

    public ObservableCollection<BarPoint> BarPoints { get; } = new();
    public ObservableCollection<LinePoint> LinePoints { get; } = new();
    public ObservableCollection<PieSlice> PieSlices { get; } = new();

    public bool HasChart => Kind != ChartKind.None;
    public bool IsBigNumber => Kind == ChartKind.BigNumber;
    public bool IsBarChart => Kind == ChartKind.BarHorizontal;
    public bool IsLineChart => Kind == ChartKind.Line;
    public bool IsPieChart => Kind == ChartKind.Pie;
    public bool IsEmpty => Kind == ChartKind.None;

    private static readonly string[] PiePalette =
    {
        "#F4C430", // DataDuck yellow
        "#10B981", // green
        "#3B82F6", // blue
        "#A855F7", // purple
        "#EC4899", // pink
        "#F97316", // orange
        "#06B6D4", // cyan
        "#84CC16", // lime
    };

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
        OnPropertyChanged(nameof(IsLineChart));
        OnPropertyChanged(nameof(IsPieChart));
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void Detect()
    {
        BarPoints.Clear();
        LinePoints.Clear();
        PieSlices.Clear();
        BigNumberValue = null;
        BigNumberLabel = null;
        CategoryColumn = null;
        ValueColumn = null;
        LineXAxisMin = null;
        LineXAxisMax = null;
        LineYAxisMin = null;
        LineYAxisMax = null;

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

        // 2) Line chart: ≥2 cols, exactly one date-ish + one numeric, ≥3 rows.
        if (cols.Count >= 2 && rows.Count >= 3 && TryBuildLineChart(cols, rows))
        {
            Kind = ChartKind.Line;
            return;
        }

        // 3) Pie chart: exactly 2 cols, one categorical + one numeric,
        //    2–8 rows, all values ≥ 0, total > 0.
        if (cols.Count == 2 && rows.Count >= 2 && rows.Count <= 8 && TryBuildPieChart(cols, rows))
        {
            Kind = ChartKind.Pie;
            return;
        }

        // 4) Bar chart: at least 2 cols, find first categorical + first numeric
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

    private bool TryBuildLineChart(IReadOnlyList<string> cols, IReadOnlyList<object?[]> rows)
    {
        // Profile each column: count date hits and numeric hits across a sample.
        var sample = Math.Min(rows.Count, 10);
        var dateCols = new List<int>();
        var numericCols = new List<int>();

        for (var c = 0; c < cols.Count; c++)
        {
            int dateHits = 0, numericHits = 0, nonNullHits = 0;
            for (var r = 0; r < sample; r++)
            {
                var v = rows[r][c];
                if (v is null) continue;
                nonNullHits++;
                if (TryAsDateMillis(v, out _, out _)) dateHits++;
                else if (TryAsDouble(v, out _)) numericHits++;
            }
            if (nonNullHits == 0) continue;
            // A column is "date-ish" if a clear majority of non-null sample values parse as dates.
            if (dateHits >= Math.Max(2, (int)Math.Ceiling(nonNullHits * 0.8))) dateCols.Add(c);
            else if (numericHits >= Math.Max(2, (int)Math.Ceiling(nonNullHits * 0.8))) numericCols.Add(c);
        }

        // Need exactly one date-ish column and at least one numeric column.
        if (dateCols.Count != 1 || numericCols.Count == 0) return false;

        var xIdx = dateCols[0];
        var yIdx = numericCols[0];

        var staged = new List<LinePoint>();
        foreach (var row in rows)
        {
            if (!TryAsDateMillis(row[xIdx], out var xMs, out var xLabel)) continue;
            if (!TryAsDouble(row[yIdx], out var y)) continue;
            staged.Add(new LinePoint
            {
                X = xMs,
                Y = y,
                XLabel = xLabel,
                YDisplay = FormatNumber(y),
            });
        }

        if (staged.Count < 3) return false;

        staged.Sort((a, b) => a.X.CompareTo(b.X));

        double xMin = staged[0].X;
        double xMax = staged[^1].X;
        double yMin = staged.Min(p => p.Y);
        double yMax = staged.Max(p => p.Y);

        if (xMin == xMax) return false;

        CategoryColumn = cols[xIdx];
        ValueColumn = cols[yIdx];
        LineXAxisMin = staged[0].XLabel;
        LineXAxisMax = staged[^1].XLabel;
        LineYAxisMin = FormatNumber(yMin);
        LineYAxisMax = FormatNumber(yMax);

        foreach (var p in staged) LinePoints.Add(p);
        return true;
    }

    private bool TryBuildPieChart(IReadOnlyList<string> cols, IReadOnlyList<object?[]> rows)
    {
        // Identify cat + numeric columns (exactly 2 cols here).
        int catIdx = -1, valIdx = -1;
        for (var c = 0; c < 2; c++)
        {
            int numericHits = 0, nonNumericHits = 0, nonNullHits = 0;
            foreach (var row in rows)
            {
                var v = row[c];
                if (v is null) continue;
                nonNullHits++;
                if (TryAsDouble(v, out _)) numericHits++;
                else nonNumericHits++;
            }
            if (nonNullHits == 0) continue;
            var isNumeric = numericHits > 0 && numericHits >= nonNumericHits;
            if (isNumeric && valIdx < 0) valIdx = c;
            else if (!isNumeric && catIdx < 0) catIdx = c;
        }
        if (catIdx < 0 || valIdx < 0) return false;

        var staged = new List<(string label, double value)>();
        double total = 0;
        foreach (var row in rows)
        {
            var label = row[catIdx]?.ToString() ?? "(null)";
            if (!TryAsDouble(row[valIdx], out var v)) return false;
            if (v < 0) return false; // pie of negatives is meaningless
            total += v;
            staged.Add((label, v));
        }

        if (staged.Count < 2 || staged.Count > 8) return false;
        if (total <= 0) return false;

        CategoryColumn = cols[catIdx];
        ValueColumn = cols[valIdx];

        double startAngle = -90.0; // start at 12 o'clock
        for (var i = 0; i < staged.Count; i++)
        {
            var (label, value) = staged[i];
            var pct = value / total * 100.0;
            var sweep = value / total * 360.0;
            PieSlices.Add(new PieSlice
            {
                Label = label,
                Value = value,
                Percent = pct,
                Display = FormatNumber(value),
                ColorHex = PiePalette[i % PiePalette.Length],
                StartAngleDeg = startAngle,
                SweepAngleDeg = sweep,
            });
            startAngle += sweep;
        }
        return true;
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

    private static bool TryAsDateMillis(object? v, out double epochMs, out string label)
    {
        switch (v)
        {
            case DateTime dt:
            {
                var u = dt.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                    : dt.ToUniversalTime();
                epochMs = (u - DateTime.UnixEpoch).TotalMilliseconds;
                label = FormatDateLabel(u);
                return true;
            }
            case DateTimeOffset dto:
            {
                epochMs = dto.ToUnixTimeMilliseconds();
                label = FormatDateLabel(dto.UtcDateTime);
                return true;
            }
            case string s when !string.IsNullOrWhiteSpace(s):
            {
                if (DateTime.TryParseExact(s, "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var d1))
                {
                    epochMs = (d1 - DateTime.UnixEpoch).TotalMilliseconds;
                    label = FormatDateLabel(d1);
                    return true;
                }
                if (DateTime.TryParse(s, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var d2))
                {
                    epochMs = (d2 - DateTime.UnixEpoch).TotalMilliseconds;
                    label = FormatDateLabel(d2);
                    return true;
                }
                break;
            }
        }
        epochMs = 0;
        label = "";
        return false;
    }

    private static string FormatDateLabel(DateTime utc)
    {
        // If midnight, show date only; otherwise show short timestamp.
        if (utc.TimeOfDay == TimeSpan.Zero)
            return utc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return utc.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
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
