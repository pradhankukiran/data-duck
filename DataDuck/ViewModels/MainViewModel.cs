using System;
using System.Linq;
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
    private readonly IAiService? _ai;
    private readonly HttpClient? _http;

    public FileListViewModel Files { get; }
    public SqlEditorTabsViewModel EditorTabs { get; }
    public ResultsViewModel Results { get; }
    public QueryHistoryViewModel History { get; }
    public SettingsViewModel Settings { get; }
    public ChartViewModel Chart { get; }
    public InsightsViewModel Insights { get; }
    public ProfileViewModel Profile { get; }
    public JoinBuilderViewModel JoinBuilder { get; }
    public ExportCommandsViewModel Export { get; }
    public DashboardViewModel Dashboard { get; }

    /// <summary>Convenience accessor to the active tab's editor (may be null briefly during refresh).</summary>
    public SqlEditorViewModel? ActiveEditor => EditorTabs.Active?.Editor;

    [ObservableProperty]
    private bool _isLoadingFile;

    [ObservableProperty]
    private string? _fileLoadError;

    [ObservableProperty]
    private LoadedFile? _activeFile;

    public MainViewModel(
        FileListViewModel files,
        SqlEditorTabsViewModel editorTabs,
        ResultsViewModel results,
        QueryHistoryViewModel history,
        SettingsViewModel settings,
        ChartViewModel chart,
        InsightsViewModel insights,
        ProfileViewModel profile,
        JoinBuilderViewModel joinBuilder,
        ExportCommandsViewModel export,
        DashboardViewModel dashboard,
        IFileService? fileService = null,
        IDuckDbService? duckDb = null,
        IAiService? ai = null,
        HttpClient? http = null)
    {
        Files = files;
        EditorTabs = editorTabs;
        Results = results;
        History = history;
        Settings = settings;
        Chart = chart;
        Insights = insights;
        Profile = profile;
        JoinBuilder = joinBuilder;
        Export = export;
        Dashboard = dashboard;
        _fileService = fileService;
        _duckDb = duckDb;
        _ai = ai;
        _http = http;

        History.QuerySelected += q => SetEditorText(q.Sql);
        Insights.QueryRunRequested += q =>
        {
            SetEditorText(q.Sql);
            var editor = EditorTabs.Active?.Editor;
            if (editor is not null && editor.RunCommand.CanExecute(null))
                editor.RunCommand.Execute(null);
        };
        JoinBuilder.JoinChosen += join => SetEditorText(join.GeneratedSql);

        Files.Files.CollectionChanged += (_, _) =>
        {
            if (ActiveFile is null && Files.Files.Count > 0)
                SetActiveFile(Files.Files[0]);
            JoinBuilder.Refresh(Files.Files);
        };
    }

    public MainViewModel() : this(
        new FileListViewModel(),
        new SqlEditorTabsViewModel(),
        new ResultsViewModel(),
        new QueryHistoryViewModel(),
        new SettingsViewModel(),
        new ChartViewModel(),
        new InsightsViewModel(),
        new ProfileViewModel(),
        new JoinBuilderViewModel(),
        new ExportCommandsViewModel(),
        new DashboardViewModel())
    { }

    private void SetEditorText(string sql)
    {
        var editor = EditorTabs.Active?.Editor;
        if (editor is null) return;
        editor.SqlText = sql;
    }

    public void SetActiveFile(LoadedFile file)
    {
        ActiveFile = file;
        _ = Profile.LoadAsync(file);
    }

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
        if (_http is null) { FileLoadError = "HttpClient unavailable."; return; }
        try
        {
            var bytes = await _http.GetByteArrayAsync("samples/sales.csv");
            await IngestAsync(new UploadedFile("sales.csv", bytes));
            SetEditorText(
                "-- 500 synthetic sales rows. Try this:\n" +
                "SELECT region, ROUND(SUM(amount), 2) AS revenue\n" +
                "FROM sales\nGROUP BY region\nORDER BY revenue DESC;");
        }
        catch (Exception ex)
        {
            FileLoadError = $"Could not load sample: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanExplain))]
    private async Task ExplainAsync()
    {
        if (_ai is null || _duckDb is null) return;
        var file = ActiveFile ?? Files.Files.FirstOrDefault();
        if (file is null) return;

        Insights.IsLoading = true;
        Insights.ErrorMessage = null;
        Insights.IsOpen = true;
        try
        {
            var sample = await _duckDb.QueryAsync($"SELECT * FROM \"{file.TableName}\" LIMIT 20");
            var insight = await _ai.ExplainDatasetAsync(file, sample.Rows);
            Insights.Load(insight);
        }
        catch (Exception ex)
        {
            Insights.ErrorMessage = ex.Message;
        }
        finally
        {
            Insights.IsLoading = false;
        }
    }

    private bool CanExplain() => _ai is not null && _duckDb is not null && Files.Files.Count > 0;

    [RelayCommand]
    private void OpenJoinBuilder()
    {
        JoinBuilder.Refresh(Files.Files);
        JoinBuilder.IsOpen = true;
    }

    [RelayCommand]
    private void PinCurrentQuery()
    {
        var editor = EditorTabs.Active?.Editor;
        if (editor is null) return;
        var sql = editor.SqlText?.Trim();
        if (string.IsNullOrWhiteSpace(sql)) return;
        var title = EditorTabs.Active?.Title ?? $"Tile {Dashboard.Tiles.Count + 1}";
        Dashboard.Pin(new DashboardTile(Guid.NewGuid(), title, sql, DateTimeOffset.Now));
    }

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
            ExplainCommand.NotifyCanExecuteChanged();
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
