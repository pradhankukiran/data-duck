using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DataDuck.Models;

namespace DataDuck.ViewModels;

public partial class ResultsViewModel : ViewModelBase
{
    [ObservableProperty]
    private long _elapsedMs;

    [ObservableProperty]
    private int _rowCount;

    public ObservableCollection<string> ColumnNames { get; } = new();
    public ObservableCollection<object?[]> Rows { get; } = new();

    public bool HasRows => Rows.Count > 0;

    /// <summary>
    /// Fires when the column set changes (after a query loads).
    /// MainView code-behind subscribes to rebuild the DataGrid's columns programmatically
    /// since results are dynamic (each query returns different shapes).
    /// </summary>
    public event Action<IReadOnlyList<string>>? ColumnsChanged;

    public ResultsViewModel()
    {
        Rows.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasRows));
            RowCount = Rows.Count;
        };
    }

    public void Load(QueryResult result)
    {
        ColumnNames.Clear();
        Rows.Clear();
        foreach (var col in result.ColumnNames) ColumnNames.Add(col);
        foreach (var row in result.Rows) Rows.Add(row);
        ElapsedMs = result.ElapsedMs;
        ColumnsChanged?.Invoke(result.ColumnNames);
    }

    public void Clear()
    {
        ColumnNames.Clear();
        Rows.Clear();
        ElapsedMs = 0;
        ColumnsChanged?.Invoke(Array.Empty<string>());
    }
}
