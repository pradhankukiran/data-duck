using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataDuck.Models;
using DataDuck.Services;

namespace DataDuck.ViewModels;

public partial class SqlEditorViewModel : ViewModelBase
{
    private readonly IDuckDbService? _duckDb;
    private readonly IAiService? _ai;
    private readonly ResultsViewModel _results;
    private readonly QueryHistoryViewModel _history;
    private readonly FileListViewModel _files;

    [ObservableProperty]
    private string _sqlText = "-- Drop a CSV in the sidebar, then type SQL here.\n-- Tip: hit Ctrl+Enter to run.\nSELECT 1 AS hello;";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _englishQuestion = string.Empty;

    [ObservableProperty]
    private bool _isGeneratingSql;

    public SqlEditorViewModel(
        ResultsViewModel results,
        QueryHistoryViewModel history,
        FileListViewModel files,
        IDuckDbService? duckDb = null,
        IAiService? ai = null)
    {
        _results = results;
        _history = history;
        _files = files;
        _duckDb = duckDb;
        _ai = ai;
    }

    // Design-time only.
    public SqlEditorViewModel() : this(
        new ResultsViewModel(),
        new QueryHistoryViewModel(),
        new FileListViewModel())
    { }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (_duckDb is null)
        {
            ErrorMessage = "DuckDB service not available (run from the Browser head).";
            return;
        }

        IsRunning = true;
        ErrorMessage = null;
        try
        {
            var result = await _duckDb.QueryAsync(SqlText);
            _results.Load(result);
            _history.Add(new SavedQuery(SqlText, DateTimeOffset.Now, result.ElapsedMs));
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsRunning = false;
        }
    }

    private bool CanRun() => !IsRunning && !string.IsNullOrWhiteSpace(SqlText);

    [RelayCommand(CanExecute = nameof(CanGenerate))]
    private async Task GenerateSqlAsync()
    {
        if (_ai is null) return;
        if (string.IsNullOrWhiteSpace(EnglishQuestion)) return;

        IsGeneratingSql = true;
        ErrorMessage = null;
        try
        {
            var sql = await _ai.GenerateSqlAsync(EnglishQuestion, _files.Files);
            SqlText = sql;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsGeneratingSql = false;
        }
    }

    private bool CanGenerate() => !IsGeneratingSql && !string.IsNullOrWhiteSpace(EnglishQuestion);

    partial void OnSqlTextChanged(string value) => RunCommand.NotifyCanExecuteChanged();
    partial void OnIsRunningChanged(bool value) => RunCommand.NotifyCanExecuteChanged();
    partial void OnEnglishQuestionChanged(string value) => GenerateSqlCommand.NotifyCanExecuteChanged();
    partial void OnIsGeneratingSqlChanged(bool value) => GenerateSqlCommand.NotifyCanExecuteChanged();
}
