using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataDuck.Models;
using DataDuck.Services;

namespace DataDuck.ViewModels;

public partial class JoinBuilderViewModel : ViewModelBase
{
    public ObservableCollection<JoinSuggestion> Suggestions { get; } = new();

    [ObservableProperty]
    private bool _isOpen;

    public bool HasSuggestions => Suggestions.Count > 0;

    /// <summary>Raised when the user accepts one of the suggested joins.</summary>
    public event Action<JoinSuggestion>? JoinChosen;

    public JoinBuilderViewModel()
    {
        Suggestions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSuggestions));
    }

    /// <summary>
    /// Recompute suggestions from the current loaded-file set. Safe to call any time
    /// the file list changes.
    /// </summary>
    public void Refresh(IReadOnlyList<LoadedFile> files)
    {
        Suggestions.Clear();
        foreach (var s in JoinSuggester.Suggest(files))
        {
            Suggestions.Add(s);
        }
    }

    [RelayCommand]
    private void Open() => IsOpen = true;

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    private void Choose(JoinSuggestion s)
    {
        JoinChosen?.Invoke(s);
        IsOpen = false;
    }
}
