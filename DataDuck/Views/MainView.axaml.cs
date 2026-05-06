using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using DataDuck.Models;
using DataDuck.Services;
using DataDuck.ViewModels;

namespace DataDuck.Views;

public partial class MainView : UserControl
{
    private ResultsViewModel? _subscribedResults;

    public MainView()
    {
        InitializeComponent();

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        TopLevelLocator.Current = TopLevel.GetTopLevel(this);
        WireResults();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        WireResults();
    }

    private void WireResults()
    {
        if (DataContext is not MainViewModel vm) return;
        if (_subscribedResults == vm.Results) return;

        if (_subscribedResults is not null)
            _subscribedResults.ColumnsChanged -= RebuildColumns;

        _subscribedResults = vm.Results;
        _subscribedResults.ColumnsChanged += RebuildColumns;

        // Initial build in case results loaded before view attached.
        RebuildColumns(_subscribedResults.ColumnNames);
    }

    private void RebuildColumns(IReadOnlyList<string> columns)
    {
        if (ResultsGrid is null) return;
        ResultsGrid.Columns.Clear();
        for (var i = 0; i < columns.Count; i++)
        {
            ResultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = columns[i],
                Binding = new Binding($"[{i}]"),
                IsReadOnly = true,
            });
        }
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var items = e.DataTransfer.TryGetFiles();
        if (items is null) return;

        foreach (var item in items.OfType<IStorageFile>())
        {
            var uploaded = await StorageProviderFileService.ReadAsync(item);
            await vm.IngestAsync(uploaded);
        }
    }

    private void OnHistorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is not ListBox lb) return;
        if (lb.SelectedItem is not SavedQuery q) return;
        vm.History.Select(q);
        lb.SelectedItem = null;  // re-clicking the same row should re-load it
    }
}
