using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ScriptHub.Helpers;
using ScriptHub.Models;
using ScriptHub.Services.Contracts;

namespace ScriptHub.ViewModels;

public enum ActiveViewType
{
    Tiles,
    Editor,
    AddScript,
    History,
    Settings
}

public class MainViewModel : ViewModelBase
{
    private readonly IScriptService _scriptService;
    private readonly IProcessService _processService;
    private readonly IHistoryService _historyService;
    private readonly IDialogService _dialogService;
    private readonly IBackupService _backupService;
    private readonly Action<ThemeMode> _onThemeChanged;

    private ActiveViewType _activeView = ActiveViewType.Tiles;
    private string _currentNavTag = "All";
    private string _searchQuery = "";
    private string _selectedCategoryFilterId = "all";
    private ScriptSortOrder _selectedSortOrder = ScriptSortOrder.CustomOrder;
    private TileSize _currentTileSize = TileSize.Standard;
    private ScriptTileViewModel? _selectedTile;
    private ScriptModel? _lastExecutedScript;

    public ObservableCollection<ScriptTileViewModel> AllTiles { get; } = new();
    public ObservableCollection<ScriptTileViewModel> FilteredTiles { get; } = new();
    public ObservableCollection<CategoryModel> Categories { get; } = new();

    public ExecutionConsoleViewModel ConsoleVM { get; }
    public ScriptEditorViewModel EditorVM { get; }
    public HistoryViewModel HistoryVM { get; }
    public SettingsViewModel SettingsVM { get; }
    public AddScriptViewModel AddScriptVM { get; }

    public ActiveViewType ActiveView
    {
        get => _activeView;
        set
        {
            if (SetProperty(ref _activeView, value))
            {
                OnPropertyChanged(nameof(IsTilesViewActive));
                OnPropertyChanged(nameof(IsEditorViewActive));
                OnPropertyChanged(nameof(IsAddScriptViewActive));
                OnPropertyChanged(nameof(IsHistoryViewActive));
                OnPropertyChanged(nameof(IsSettingsViewActive));
            }
        }
    }

    public bool IsTilesViewActive => ActiveView == ActiveViewType.Tiles;
    public bool IsEditorViewActive => ActiveView == ActiveViewType.Editor;
    public bool IsAddScriptViewActive => ActiveView == ActiveViewType.AddScript;
    public bool IsHistoryViewActive => ActiveView == ActiveViewType.History;
    public bool IsSettingsViewActive => ActiveView == ActiveViewType.Settings;

    public string CurrentNavTag
    {
        get => _currentNavTag;
        set
        {
            if (SetProperty(ref _currentNavTag, value))
            {
                if (value == "History")
                {
                    ActiveView = ActiveViewType.History;
                    _ = HistoryVM.LoadHistoryAsync();
                }
                else if (value == "Settings")
                {
                    ActiveView = ActiveViewType.Settings;
                    SettingsVM.LoadSettings();
                }
                else
                {
                    ActiveView = ActiveViewType.Tiles;
                    ApplyFilters();
                }
            }
        }
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (SetProperty(ref _searchQuery, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedCategoryFilterId
    {
        get => _selectedCategoryFilterId;
        set
        {
            if (SetProperty(ref _selectedCategoryFilterId, value))
            {
                ApplyFilters();
            }
        }
    }

    public ScriptSortOrder SelectedSortOrder
    {
        get => _selectedSortOrder;
        set
        {
            if (SetProperty(ref _selectedSortOrder, value))
            {
                ApplyFilters();
            }
        }
    }

    public TileSize CurrentTileSize
    {
        get => _currentTileSize;
        set
        {
            if (SetProperty(ref _currentTileSize, value))
            {
                foreach (var tile in AllTiles)
                {
                    tile.Model.TileSize = value;
                    tile.RefreshProperties();
                }
            }
        }
    }

    public ScriptTileViewModel? SelectedTile
    {
        get => _selectedTile;
        set => SetProperty(ref _selectedTile, value);
    }

    public int TotalScriptsCount => AllTiles.Count;
    public int FilteredScriptsCount => FilteredTiles.Count;

    // Commands
    public ICommand NavigateCommand { get; }
    public ICommand ShowAddScriptCommand { get; }
    public ICommand RunSelectedScriptCommand { get; }
    public ICommand RunLastScriptCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SetTileSizeCommand { get; }
    public ICommand DeleteSelectedCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    public MainViewModel(
        IScriptService scriptService,
        IProcessService processService,
        IHistoryService historyService,
        IDialogService dialogService,
        IBackupService backupService,
        Action<ThemeMode> onThemeChanged)
    {
        _scriptService = scriptService;
        _processService = processService;
        _historyService = historyService;
        _dialogService = dialogService;
        _backupService = backupService;
        _onThemeChanged = onThemeChanged;

        ConsoleVM = new ExecutionConsoleViewModel(_processService);
        HistoryVM = new HistoryViewModel(_historyService, _dialogService, RerunScriptById);
        SettingsVM = new SettingsViewModel(_scriptService, _backupService, _dialogService, _onThemeChanged, () => _ = InitializeAsync());
        
        EditorVM = new ScriptEditorViewModel(
            _scriptService, 
            _dialogService, 
            OnScriptSaved, 
            () => ActiveView = ActiveViewType.Tiles);

        AddScriptVM = new AddScriptViewModel(
            _dialogService,
            OnAddScriptOptionSelected,
            () => ActiveView = ActiveViewType.Tiles);

        NavigateCommand = new RelayCommand<string>(tag => CurrentNavTag = tag ?? "All");
        ShowAddScriptCommand = new RelayCommand(() => ActiveView = ActiveViewType.AddScript);
        RunSelectedScriptCommand = new RelayCommand(RunSelectedScript);
        RunLastScriptCommand = new RelayCommand(RunLastScript);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        SetTileSizeCommand = new RelayCommand<TileSize>(size => CurrentTileSize = size);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
    }

    public async Task InitializeAsync()
    {
        var config = _scriptService.GetConfig();
        CurrentTileSize = config.DefaultTileSize;
        SelectedSortOrder = config.DefaultSortOrder;

        ReloadCategories();
        await ReloadScriptsAsync();
    }

    private void ReloadCategories()
    {
        Categories.Clear();
        Categories.Add(new CategoryModel { Id = "all", Name = "Все категории", ColorHex = "#0078D4" });
        foreach (var cat in _scriptService.GetAllCategories())
        {
            Categories.Add(cat);
        }
    }

    public async Task ReloadScriptsAsync()
    {
        AllTiles.Clear();
        var scripts = _scriptService.GetAllScripts();
        var categories = _scriptService.GetAllCategories();

        foreach (var s in scripts)
        {
            var cat = categories.FirstOrDefault(c => c.Id == s.CategoryId);
            var tileVm = new ScriptTileViewModel(
                s,
                _scriptService,
                ExecuteScript,
                EditScript,
                DeleteScript,
                OnFavoriteChanged)
            {
                CategoryName = cat?.Name ?? "Общее",
                CategoryColor = cat?.ColorHex ?? "#0078D4"
            };
            AllTiles.Add(tileVm);
        }

        ApplyFilters();
        OnPropertyChanged(nameof(TotalScriptsCount));
        await Task.CompletedTask;
    }

    public void ApplyFilters()
    {
        FilteredTiles.Clear();

        var query = AllTiles.AsEnumerable();

        // 1. Navigation view filter (All, PowerShell, Batch, Favorites)
        if (CurrentNavTag == "PowerShell")
        {
            query = query.Where(t => t.ScriptType == ScriptType.PowerShell);
        }
        else if (CurrentNavTag == "Batch")
        {
            query = query.Where(t => t.ScriptType == ScriptType.Batch || t.ScriptType == ScriptType.Cmd);
        }
        else if (CurrentNavTag == "Favorites")
        {
            query = query.Where(t => t.IsFavorite);
        }

        // 2. Category filter
        if (!string.IsNullOrEmpty(SelectedCategoryFilterId) && SelectedCategoryFilterId != "all")
        {
            query = query.Where(t => t.CategoryId == SelectedCategoryFilterId);
        }

        // 3. Search query filter
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            var q = SearchQuery.Trim().ToLowerInvariant();
            query = query.Where(t => 
                (t.Title?.ToLowerInvariant().Contains(q) ?? false) ||
                (t.Description?.ToLowerInvariant().Contains(q) ?? false) ||
                (t.CategoryName?.ToLowerInvariant().Contains(q) ?? false) ||
                (t.FilePath?.ToLowerInvariant().Contains(q) ?? false) ||
                (t.Model.Arguments?.ToLowerInvariant().Contains(q) ?? false));
        }

        // 4. Sorting (Pinned favorites always on top if in All or Type view)
        var ordered = SelectedSortOrder switch
        {
            ScriptSortOrder.NameAsc => query.OrderByDescending(t => t.IsFavorite).ThenBy(t => t.Title),
            ScriptSortOrder.NameDesc => query.OrderByDescending(t => t.IsFavorite).ThenByDescending(t => t.Title),
            ScriptSortOrder.ByType => query.OrderByDescending(t => t.IsFavorite).ThenBy(t => t.ScriptType).ThenBy(t => t.Title),
            ScriptSortOrder.ByCategory => query.OrderByDescending(t => t.IsFavorite).ThenBy(t => t.CategoryName).ThenBy(t => t.Title),
            ScriptSortOrder.ByLastRun => query.OrderByDescending(t => t.IsFavorite).ThenByDescending(t => t.LastRunAt ?? DateTime.MinValue),
            _ => query.OrderByDescending(t => t.IsFavorite).ThenBy(t => t.OrderIndex)
        };

        foreach (var item in ordered)
        {
            FilteredTiles.Add(item);
        }

        OnPropertyChanged(nameof(FilteredScriptsCount));
    }

    public async void ExecuteScript(ScriptTileViewModel tile)
    {
        var script = tile.Model;
        _lastExecutedScript = script;
        SelectedTile = tile;

        ConsoleVM.PrepareForExecution(script);
        tile.LastStatus = ExecutionStatus.Running;

        try
        {
            var result = await _processService.ExecuteScriptAsync(
                script, 
                (line, isErr) => ConsoleVM.AppendLog(line, isErr));

            ConsoleVM.FinishExecution(result);

            tile.LastStatus = result.Status;
            tile.LastRunAt = result.EndTime;
            tile.LastDuration = result.Duration;
            script.LastExitCode = result.ExitCode;
            script.LastRunAt = result.EndTime;
            script.LastDuration = result.Duration;
            script.LastStatus = result.Status;

            await _scriptService.SaveScriptAsync(script);

            // Record to History
            var historyEntry = new HistoryEntryModel
            {
                Id = Guid.NewGuid().ToString(),
                ScriptId = script.Id,
                ScriptTitle = script.Title,
                ScriptType = script.ScriptType,
                ScriptFilePath = script.FilePath,
                Arguments = script.Arguments,
                WorkingDirectory = _processService.ResolveWorkingDirectory(script),
                RunAsAdmin = script.RunAsAdmin,
                StartTime = result.StartTime,
                EndTime = result.EndTime,
                Duration = result.Duration,
                ExitCode = result.ExitCode,
                Status = result.Status,
                LogSnippet = result.Output.Length > 200 ? result.Output[..200] + "..." : result.Output
            };

            await _historyService.AddHistoryEntryAsync(historyEntry);
        }
        catch (Exception ex)
        {
            tile.LastStatus = ExecutionStatus.Failed;
            ConsoleVM.AppendLog($"Исключение при выполнении: {ex.Message}", true);
        }
    }

    public void EditScript(ScriptTileViewModel tile)
    {
        EditorVM.LoadForEdit(tile.Model);
        ActiveView = ActiveViewType.Editor;
    }

    public async void DeleteScript(ScriptTileViewModel tile)
    {
        var result = await _dialogService.ShowDeleteConfirmationAsync(tile.Title);
        if (result.Confirmed)
        {
            await _scriptService.DeleteScriptAsync(tile.Id, result.DeletePhysicalFile);
            AllTiles.Remove(tile);
            FilteredTiles.Remove(tile);
            OnPropertyChanged(nameof(TotalScriptsCount));
            OnPropertyChanged(nameof(FilteredScriptsCount));
        }
    }

    public void HandleFileDrop(string[] files)
    {
        if (files == null || files.Length == 0) return;

        var firstValid = files.FirstOrDefault(f =>
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            return ext is ".ps1" or ".bat" or ".cmd";
        });

        if (firstValid != null)
        {
            var ext = Path.GetExtension(firstValid).ToLowerInvariant();
            var scriptType = ext switch
            {
                ".bat" => ScriptType.Batch,
                ".cmd" => ScriptType.Cmd,
                _ => ScriptType.PowerShell
            };

            EditorVM.LoadForCreate(scriptType, firstValid);
            ActiveView = ActiveViewType.Editor;
        }
    }

    private void OnAddScriptOptionSelected(ScriptType scriptType, string? importedFilePath)
    {
        EditorVM.LoadForCreate(scriptType, importedFilePath);
        ActiveView = ActiveViewType.Editor;
    }

    private async void OnScriptSaved(ScriptModel script, bool thenRun)
    {
        await ReloadScriptsAsync();
        ActiveView = ActiveViewType.Tiles;

        if (thenRun)
        {
            var tile = AllTiles.FirstOrDefault(t => t.Id == script.Id);
            if (tile != null)
            {
                ExecuteScript(tile);
            }
        }
    }

    private void OnFavoriteChanged(ScriptTileViewModel tile)
    {
        _ = _scriptService.SaveScriptAsync(tile.Model);
        ApplyFilters();
    }

    private void RerunScriptById(string scriptId)
    {
        var tile = AllTiles.FirstOrDefault(t => t.Id == scriptId);
        if (tile != null)
        {
            ActiveView = ActiveViewType.Tiles;
            ExecuteScript(tile);
        }
    }

    private void RunSelectedScript()
    {
        if (SelectedTile != null)
        {
            ExecuteScript(SelectedTile);
        }
        else if (FilteredTiles.Count > 0)
        {
            ExecuteScript(FilteredTiles[0]);
        }
    }

    private void RunLastScript()
    {
        if (_lastExecutedScript != null)
        {
            var tile = AllTiles.FirstOrDefault(t => t.Id == _lastExecutedScript.Id);
            if (tile != null)
            {
                ExecuteScript(tile);
                return;
            }
        }
        RunSelectedScript();
    }

    private async Task RefreshAsync()
    {
        foreach (var tile in AllTiles)
        {
            tile.RefreshFileStatus();
        }
        await Task.CompletedTask;
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedTile != null)
        {
            DeleteScript(SelectedTile);
        }
        await Task.CompletedTask;
    }

    public void CollapseAllTiles()
    {
        foreach (var tile in AllTiles)
        {
            tile.IsExpanded = false;
        }
    }

    private void ToggleTheme()
    {
        var nextTheme = SettingsVM.SelectedTheme == ThemeMode.Dark ? ThemeMode.Light : ThemeMode.Dark;
        SettingsVM.SelectedTheme = nextTheme;
    }
}
