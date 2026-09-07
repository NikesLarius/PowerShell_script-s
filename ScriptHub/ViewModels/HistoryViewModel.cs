using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ScriptHub.Helpers;
using ScriptHub.Models;
using ScriptHub.Services.Contracts;

namespace ScriptHub.ViewModels;

public class HistoryViewModel : ViewModelBase
{
    private readonly IHistoryService _historyService;
    private readonly IDialogService _dialogService;
    private readonly Action<string> _onRerunScript;

    private string _searchQuery = "";
    private HistoryEntryModel? _selectedEntry;

    public ObservableCollection<HistoryEntryModel> FilteredItems { get; } = new();
    private readonly ObservableCollection<HistoryEntryModel> _allItems = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFilter();
            }
        }
    }

    public HistoryEntryModel? SelectedEntry
    {
        get => _selectedEntry;
        set => SetProperty(ref _selectedEntry, value);
    }

    public ICommand ClearHistoryCommand { get; }
    public ICommand DeleteEntryCommand { get; }
    public ICommand RerunCommand { get; }
    public ICommand RefreshCommand { get; }

    public HistoryViewModel(
        IHistoryService historyService,
        IDialogService dialogService,
        Action<string> onRerunScript)
    {
        _historyService = historyService;
        _dialogService = dialogService;
        _onRerunScript = onRerunScript;

        ClearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync);
        DeleteEntryCommand = new AsyncRelayCommand(DeleteSelectedEntryAsync);
        RerunCommand = new RelayCommand(() =>
        {
            if (SelectedEntry != null && !string.IsNullOrWhiteSpace(SelectedEntry.ScriptId))
            {
                _onRerunScript(SelectedEntry.ScriptId);
            }
        });
        RefreshCommand = new AsyncRelayCommand(LoadHistoryAsync);
    }

    public async Task LoadHistoryAsync()
    {
        var items = await _historyService.GetHistoryAsync();
        _allItems.Clear();
        foreach (var item in items)
        {
            _allItems.Add(item);
        }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredItems.Clear();
        var q = _searchQuery?.Trim().ToLowerInvariant() ?? "";
        
        var matches = string.IsNullOrWhiteSpace(q)
            ? _allItems
            : _allItems.Where(i => 
                (i.ScriptTitle?.ToLowerInvariant().Contains(q) ?? false) ||
                (i.ScriptFilePath?.ToLowerInvariant().Contains(q) ?? false) ||
                (i.Arguments?.ToLowerInvariant().Contains(q) ?? false));

        foreach (var match in matches)
        {
            FilteredItems.Add(match);
        }
    }

    private async Task ClearHistoryAsync()
    {
        var confirm = await _dialogService.ShowConfirmationAsync(
            "Очистка истории", 
            "Вы уверены, что хотите полностью очистить журнал запусков?");

        if (confirm)
        {
            await _historyService.ClearHistoryAsync();
            _allItems.Clear();
            FilteredItems.Clear();
        }
    }

    private async Task DeleteSelectedEntryAsync()
    {
        if (SelectedEntry == null) return;
        await _historyService.DeleteHistoryEntryAsync(SelectedEntry.Id);
        _allItems.Remove(SelectedEntry);
        FilteredItems.Remove(SelectedEntry);
    }
}
