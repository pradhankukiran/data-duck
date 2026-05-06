using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
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
        RebuildColumns(_subscribedResults.ColumnNames);
    }

    private void RebuildColumns(IReadOnlyList<string> columns)
    {
        if (ResultsGrid is null) return;
        ResultsGrid.Columns.Clear();
        for (var i = 0; i < columns.Count; i++)
        {
            var idx = i;
            ResultsGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = columns[i],
                CellTemplate = new FuncDataTemplate<object?[]>((_, _) =>
                {
                    var tb = new TextBlock
                    {
                        Margin = new Thickness(8, 4),
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    tb.Bind(TextBlock.TextProperty, new Binding($"[{idx}]"));
                    tb.Bind(TextBlock.ForegroundProperty, new Binding($"[{idx}]")
                    {
                        Converter = NegativeNumberToRedConverter.Instance,
                    });
                    return tb;
                }, supportsRecycling: true),
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
        lb.SelectedItem = null;
    }

    private async void OnCopyCsv(object? sender, RoutedEventArgs e) =>
        await CopyAsync(vm => vm.Export.CopyCsv());
    private async void OnCopyTsv(object? sender, RoutedEventArgs e) =>
        await CopyAsync(vm => vm.Export.CopyTsv());
    private async void OnCopyMarkdown(object? sender, RoutedEventArgs e) =>
        await CopyAsync(vm => vm.Export.CopyMarkdown());

    private async Task CopyAsync(Func<MainViewModel, string> getter)
    {
        if (DataContext is not MainViewModel vm) return;
        var text = getter(vm);
        var top = TopLevel.GetTopLevel(this);
        if (top?.Clipboard is null) return;
        await top.Clipboard.SetTextAsync(text);
    }
}

internal sealed class NegativeNumberToRedConverter : IValueConverter
{
    public static readonly NegativeNumberToRedConverter Instance = new();
    private static readonly IBrush Red = new SolidColorBrush(Color.FromRgb(220, 38, 38));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var d = ToDouble(value);
        if (double.IsNaN(d)) return AvaloniaProperty.UnsetValue;
        return d < 0 ? Red : AvaloniaProperty.UnsetValue;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static double ToDouble(object? v) => v switch
    {
        null => double.NaN,
        double d => d,
        float f => f,
        long l => l,
        int i => i,
        short s => s,
        decimal m => (double)m,
        string str when double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) => p,
        _ => double.NaN,
    };
}
