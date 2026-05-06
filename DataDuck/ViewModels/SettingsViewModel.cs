using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataDuck.Services;

namespace DataDuck.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    public const string GroqKeyName = "dataduck.groq.apikey";

    private readonly ILocalStore? _store;

    [ObservableProperty]
    private string? _groqApiKey;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string? _statusMessage;

    public SettingsViewModel(ILocalStore? store = null)
    {
        _store = store;
        _groqApiKey = _store?.Get(GroqKeyName);
    }

    [RelayCommand]
    private void Open()
    {
        StatusMessage = null;
        IsOpen = true;
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private void Save()
    {
        if (_store is null)
        {
            StatusMessage = "Storage unavailable.";
            return;
        }
        if (string.IsNullOrWhiteSpace(GroqApiKey))
        {
            _store.Remove(GroqKeyName);
            StatusMessage = "Key cleared.";
        }
        else
        {
            _store.Set(GroqKeyName, GroqApiKey.Trim());
            StatusMessage = "Saved. Closing in a moment…";
        }
        IsOpen = false;
    }

    [RelayCommand]
    private void Clear()
    {
        GroqApiKey = string.Empty;
        _store?.Remove(GroqKeyName);
        StatusMessage = "Key cleared.";
    }
}
