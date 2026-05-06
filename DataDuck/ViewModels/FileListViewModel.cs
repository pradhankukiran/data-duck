using System.Collections.ObjectModel;
using Avalonia.Controls;
using DataDuck.Models;

namespace DataDuck.ViewModels;

public partial class FileListViewModel : ViewModelBase
{
    public ObservableCollection<LoadedFile> Files { get; } = new();

    public bool HasFiles => Files.Count > 0;

    public FileListViewModel()
    {
        Files.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasFiles));

        // Design-time only: populate the previewer / dev runtime with realistic-looking
        // entries so the empty layout doesn't look broken before any file is loaded.
        if (Design.IsDesignMode)
        {
            SeedDummyData();
        }
    }

    private void SeedDummyData()
    {
        Files.Add(new LoadedFile(
            "sales_2026_q1.csv", "sales_2026_q1", 4_521_000, 124_532,
            new[]
            {
                new ColumnMeta("order_date", "DATE"),
                new ColumnMeta("region", "VARCHAR"),
                new ColumnMeta("customer_id", "BIGINT"),
                new ColumnMeta("amount", "DECIMAL"),
            }));

        Files.Add(new LoadedFile(
            "customers.parquet", "customers", 1_182_440, 8_421,
            new[]
            {
                new ColumnMeta("id", "BIGINT"),
                new ColumnMeta("name", "VARCHAR"),
                new ColumnMeta("country", "VARCHAR"),
            }));
    }
}
