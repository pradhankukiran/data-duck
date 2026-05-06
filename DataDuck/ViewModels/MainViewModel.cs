using System;
using System.Net.Http;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataDuck.Models;
using DataDuck.Services;

namespace DataDuck.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IFileService? _fileService;
    private readonly IDuckDbService? _duckDb;
    private readonly HttpClient? _http;

    public FileListViewModel Files { get; }
    public SqlEditorViewModel Editor { get; }
    public ResultsViewModel Results { get; }
    public QueryHistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private bool _isLoadingFile;

    [ObservableProperty]
    private string? _fileLoadError;

    public MainViewModel(
        FileListViewModel files,
        SqlEditorViewModel editor,
        ResultsViewModel results,
        QueryHistoryViewModel history,
        SettingsViewModel settings,
        IFileService? fileService = null,
        IDuckDbService? duckDb = null,
        HttpClient? http = null)
    {
        Files = files;
        Editor = editor;
        Results = results;
        History = history;
        Settings = settings;
        _fileService = fileService;
        _duckDb = duckDb;
        _http = http;

        // Click a history entry → load its SQL into the editor.
        History.QuerySelected += q => Editor.SqlText = q.Sql;
    }

    // Design-time only — gives the XAML previewer something to bind to.
    public MainViewModel() : this(
        new FileListViewModel(),
        new SqlEditorViewModel(),
        new ResultsViewModel(),
        new QueryHistoryViewModel(),
        new SettingsViewModel())
    { }

    [RelayCommand]
    private async Task OpenFileAsync()
    {
        if (_fileService is null) return;
        var picked = await _fileService.PickAsync();
        if (picked is null) return;
        await IngestAsync(picked);
    }

    [RelayCommand]
    private async Task LoadSampleAsync()
    {
        if (_http is null)
        {
            FileLoadError = "HttpClient unavailable.";
            return;
        }
        try
        {
            var bytes = await _http.GetByteArrayAsync("samples/sales.csv");
            await IngestAsync(new UploadedFile("sales.csv", bytes));
            Editor.SqlText =
                "-- 500 synthetic sales rows. Try this:\n" +
                "SELECT region, COUNT(*) AS orders, ROUND(SUM(amount), 2) AS revenue\n" +
                "FROM sales\nGROUP BY region\nORDER BY revenue DESC;";
        }
        catch (Exception ex)
        {
            FileLoadError = $"Could not load sample: {ex.Message}";
        }
    }

    /// <summary>
    /// Ingest an uploaded file: hand it to DuckDB-WASM (which auto-creates a SQL view),
    /// then add the populated LoadedFile (with real columns + row count) to the sidebar.
    /// First call triggers the ~33 MB DuckDB-WASM bundle download — UI shows a spinner.
    /// </summary>
    public async Task IngestAsync(UploadedFile file)
    {
        IsLoadingFile = true;
        FileLoadError = null;
        try
        {
            if (_duckDb is null)
            {
                Files.Files.Add(new LoadedFile(
                    file.Name, ToTableName(file.Name), file.Data.LongLength, 0, Array.Empty<ColumnMeta>()));
                return;
            }

            var loaded = await _duckDb.RegisterFileAsync(file.Name, file.Data);
            Files.Files.Add(loaded);
        }
        catch (Exception ex)
        {
            FileLoadError = $"Failed to load {file.Name}: {ex.Message}";
        }
        finally
        {
            IsLoadingFile = false;
        }
    }

    private static string ToTableName(string fileName)
    {
        var dot = fileName.LastIndexOf('.');
        var stem = dot > 0 ? fileName[..dot] : fileName;
        var chars = stem.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                chars[i] = '_';
        var name = new string(chars);
        if (name.Length == 0 || char.IsDigit(name[0]))
            name = "t_" + name;
        return name;
    }
}
