using System.Linq;
using DataDuck.Models;
using DataDuck.Services;

namespace DataDuck.ViewModels;

/// <summary>
/// Thin facade that lets a parent VM/View ask for the current results in a particular
/// text format. Clipboard interaction is intentionally not done here — that requires
/// <c>TopLevel.Clipboard</c>, which is a view-layer concern.
/// </summary>
public partial class ExportCommandsViewModel : ViewModelBase
{
    private readonly ResultsViewModel _results;

    public ExportCommandsViewModel(ResultsViewModel results)
    {
        _results = results;
    }

    public ExportCommandsViewModel() : this(new ResultsViewModel())
    {
    }

    public bool CanExport => _results.HasRows;

    private QueryResult Snapshot() => new(
        _results.ColumnNames.ToArray(),
        _results.Rows.ToArray(),
        _results.ElapsedMs);

    public string CopyCsv() => ResultExporter.ToCsv(Snapshot());
    public string CopyTsv() => ResultExporter.ToTsv(Snapshot());
    public string CopyMarkdown() => ResultExporter.ToMarkdown(Snapshot());
}
