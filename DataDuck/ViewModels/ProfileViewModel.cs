using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using DataDuck.Models;
using DataDuck.Services;

namespace DataDuck.ViewModels;

public partial class ProfileViewModel : ViewModelBase
{
    private readonly IProfilingService? _profiler;

    [ObservableProperty]
    private LoadedFile? _activeFile;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public ObservableCollection<ColumnProfile> Columns { get; } = new();

    public bool HasProfile => Columns.Count > 0;

    public ProfileViewModel(IProfilingService? profiler = null)
    {
        _profiler = profiler;
        Columns.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasProfile));

        if (Design.IsDesignMode)
        {
            SeedDummyData();
        }
    }

    // Design-time / parameterless ctor for the XAML previewer.
    public ProfileViewModel() : this(null) { }

    public async Task LoadAsync(LoadedFile file)
    {
        if (file is null) throw new ArgumentNullException(nameof(file));

        Clear();
        ActiveFile = file;

        if (_profiler is null)
        {
            ErrorMessage = "Profiling service not available.";
            return;
        }

        IsLoading = true;
        try
        {
            var profiles = await _profiler.ProfileAsync(file);
            foreach (var p in profiles) Columns.Add(p);
        }
        catch (NotSupportedException ex)
        {
            // Desktop head's NotSupportedDuckDbService throws this on QueryAsync.
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Profiling failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void Clear()
    {
        Columns.Clear();
        ActiveFile = null;
        ErrorMessage = null;
        IsLoading = false;
    }

    private void SeedDummyData()
    {
        ActiveFile = new LoadedFile(
            "sales_2026_q1.csv", "sales_2026_q1", 4_521_000, 124_532,
            new[]
            {
                new ColumnMeta("region", "VARCHAR"),
                new ColumnMeta("amount", "DECIMAL"),
            });

        Columns.Add(new ColumnProfile(
            ColumnName: "region",
            DataType: "VARCHAR",
            DistinctCount: 5,
            NullCount: 12,
            TotalCount: 124_532,
            MinValue: "APAC",
            MaxValue: "US",
            Mean: null,
            StdDev: null,
            TopValues: new[]
            {
                new TopValue("US", 41_204),
                new TopValue("EU", 38_910),
                new TopValue("APAC", 22_117),
                new TopValue("LATAM", 14_223),
                new TopValue("MEA", 8_066),
            }));

        Columns.Add(new ColumnProfile(
            ColumnName: "amount",
            DataType: "DECIMAL",
            DistinctCount: 92_113,
            NullCount: 0,
            TotalCount: 124_532,
            MinValue: "0.99",
            MaxValue: "9999.50",
            Mean: 248.71,
            StdDev: 412.04,
            TopValues: new[]
            {
                new TopValue("19.99", 1_204),
                new TopValue("29.99", 988),
            }));
    }
}
