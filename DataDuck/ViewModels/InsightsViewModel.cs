using System;
using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataDuck.Models;

namespace DataDuck.ViewModels;

public partial class InsightsViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _summary;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isOpen;

    public ObservableCollection<string> Findings { get; } = new();
    public ObservableCollection<SuggestedQuery> SuggestedQueries { get; } = new();

    public bool HasInsight => !string.IsNullOrEmpty(Summary);

    /// <summary>Raised when the user clicks "Run" on a suggested query.</summary>
    public event Action<SuggestedQuery>? QueryRunRequested;

    public InsightsViewModel()
    {
        if (Design.IsDesignMode)
        {
            SeedDummyData();
        }
    }

    public void Load(DatasetInsight insight)
    {
        ErrorMessage = null;
        Findings.Clear();
        SuggestedQueries.Clear();

        Summary = insight.Summary;
        foreach (var finding in insight.Findings) Findings.Add(finding);
        foreach (var q in insight.SuggestedQueries) SuggestedQueries.Add(q);

        OnPropertyChanged(nameof(HasInsight));
    }

    public void Clear()
    {
        Summary = null;
        ErrorMessage = null;
        Findings.Clear();
        SuggestedQueries.Clear();
        OnPropertyChanged(nameof(HasInsight));
    }

    [RelayCommand]
    private void RunSuggested(SuggestedQuery q)
    {
        if (q is null) return;
        IsOpen = false;
        QueryRunRequested?.Invoke(q);
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    partial void OnSummaryChanged(string? value) => OnPropertyChanged(nameof(HasInsight));

    private void SeedDummyData()
    {
        Summary = "A small Q1 sales table covering 4 regions with order dates, customers, and amounts.";
        Findings.Add("Most orders fall in the 50-500 USD range; a long tail extends past 5,000 USD.");
        Findings.Add("The 'region' column has 4 distinct values; 'NA' dominates.");
        Findings.Add("There are 12 rows with null customer_id — likely guest checkouts.");
        SuggestedQueries.Add(new SuggestedQuery("Total revenue by region", "SELECT region, SUM(amount) AS total FROM sales_2026_q1 GROUP BY region ORDER BY total DESC;"));
        SuggestedQueries.Add(new SuggestedQuery("Daily order volume", "SELECT order_date, COUNT(*) AS orders FROM sales_2026_q1 GROUP BY order_date ORDER BY order_date;"));
        SuggestedQueries.Add(new SuggestedQuery("Top 10 customers", "SELECT customer_id, SUM(amount) AS spend FROM sales_2026_q1 GROUP BY customer_id ORDER BY spend DESC LIMIT 10;"));
    }
}
