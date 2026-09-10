using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using ScriptHub.Helpers;
using ScriptHub.Models;
using ScriptHub.Services.Contracts;

namespace ScriptHub.ViewModels;

public class ScriptEditorViewModel : ViewModelBase
{
    private readonly IScriptService _scriptService;
    private readonly IDialogService _dialogService;
    private readonly Action<ScriptModel, bool> _onSaveCompleted;
    private readonly Action _onCancel;

    private ScriptModel _editingScript = new();
    private bool _isNewScript;
    private string _codeContent = "";
    private string _title = "";
    private string _description = "";
    private string _filePath = "";
    private ScriptType _scriptType = ScriptType.PowerShell;
    private string _selectedCategoryId = "";
    private string _icon = "DocumentCode24";
    private string _accentColor = "#0078D4";
    private TileSize _tileSize = TileSize.Standard;
    private bool _runAsAdmin;
    private WorkingDirectoryMode _workingDirectoryMode = WorkingDirectoryMode.ScriptDirectory;
    private string _customWorkingDirectory = "";
    private string _arguments = "";
    private string _validationError = "";

    public bool IsNewScript
    {
        get => _isNewScript;
        set => SetProperty(ref _isNewScript, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    public ScriptType ScriptType
    {
        get => _scriptType;
        set
        {
            if (SetProperty(ref _scriptType, value))
            {
                OnPropertyChanged(nameof(SyntaxName));
            }
        }
    }

    public string SyntaxName => ScriptType switch
    {
        ScriptType.PowerShell => "PowerShell",
        ScriptType.Batch or ScriptType.Cmd => "Batch",
        _ => "PowerShell"
    };

    public string SelectedCategoryId
    {
        get => _selectedCategoryId;
        set => SetProperty(ref _selectedCategoryId, value);
    }

    public string Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    public string AccentColor
    {
        get => _accentColor;
        set => SetProperty(ref _accentColor, value);
    }

    public TileSize TileSize
    {
        get => _tileSize;
        set => SetProperty(ref _tileSize, value);
    }

    public bool RunAsAdmin
    {
        get => _runAsAdmin;
        set => SetProperty(ref _runAsAdmin, value);
    }

    public WorkingDirectoryMode WorkingDirectoryMode
    {
        get => _workingDirectoryMode;
        set
        {
            if (SetProperty(ref _workingDirectoryMode, value))
            {
                OnPropertyChanged(nameof(IsCustomWorkDirVisible));
            }
        }
    }

    public bool IsCustomWorkDirVisible => WorkingDirectoryMode == WorkingDirectoryMode.CustomDirectory;

    public string CustomWorkingDirectory
    {
        get => _customWorkingDirectory;
        set => SetProperty(ref _customWorkingDirectory, value);
    }

    public string Arguments
    {
        get => _arguments;
        set => SetProperty(ref _arguments, value);
    }

    public string CodeContent
    {
        get => _codeContent;
        set => SetProperty(ref _codeContent, value);
    }

    public string ValidationError
    {
        get => _validationError;
        set => SetProperty(ref _validationError, value);
    }

    public ObservableCollection<CategoryModel> Categories { get; } = new();

    public ICommand SaveCommand { get; }
    public ICommand SaveAndRunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseScriptFileCommand { get; }
    public ICommand BrowseCustomWorkDirCommand { get; }

    public ScriptEditorViewModel(
        IScriptService scriptService,
        IDialogService dialogService,
        Action<ScriptModel, bool> onSaveCompleted,
        Action onCancel)
    {
        _scriptService = scriptService;
        _dialogService = dialogService;
        _onSaveCompleted = onSaveCompleted;
        _onCancel = onCancel;

        SaveCommand = new AsyncRelayCommand(async () => await SaveAsync(false));
        SaveAndRunCommand = new AsyncRelayCommand(async () => await SaveAsync(true));
        CancelCommand = new RelayCommand(_onCancel);

        BrowseScriptFileCommand = new RelayCommand(BrowseScriptFile);
        BrowseCustomWorkDirCommand = new RelayCommand(BrowseCustomWorkDir);
    }

    public async void LoadForEdit(ScriptModel script)
    {
        _editingScript = script.Clone();
        IsNewScript = false;

        Title = script.Title;
        Description = script.Description;
        FilePath = script.FilePath;
        ScriptType = script.ScriptType;
        SelectedCategoryId = script.CategoryId;
        Icon = script.Icon;
        AccentColor = script.AccentColor;
        TileSize = script.TileSize;
        RunAsAdmin = script.RunAsAdmin;
        WorkingDirectoryMode = script.WorkingDirectoryMode;
        CustomWorkingDirectory = script.CustomWorkingDirectory;
        Arguments = script.Arguments;
        ValidationError = "";

        RefreshCategories();

        if (File.Exists(script.FilePath))
        {
            CodeContent = await _scriptService.LoadScriptContentAsync(script.FilePath);
        }
        else
        {
            CodeContent = "";
        }
    }

    public async void LoadForCreate(ScriptType defaultType = ScriptType.PowerShell, string? importedFilePath = null)
    {
        _editingScript = new ScriptModel
        {
            Id = Guid.NewGuid().ToString(),
            ScriptType = defaultType,
            CreatedAt = DateTime.UtcNow
        };
        IsNewScript = true;

        RefreshCategories();

        if (!string.IsNullOrWhiteSpace(importedFilePath) && File.Exists(importedFilePath))
        {
            var ext = Path.GetExtension(importedFilePath).ToLowerInvariant();
            var detectedType = ext switch
            {
                ".bat" => ScriptType.Batch,
                ".cmd" => ScriptType.Cmd,
                _ => ScriptType.PowerShell
            };

            Title = Path.GetFileNameWithoutExtension(importedFilePath);
            Description = "Импортированный скрипт";
            FilePath = importedFilePath;
            ScriptType = detectedType;
            SelectedCategoryId = Categories.FirstOrDefault()?.Id ?? "";
            Icon = "DocumentCode24";
            AccentColor = detectedType == ScriptType.PowerShell ? "#0078D4" : "#D83B01";
            TileSize = TileSize.Standard;
            RunAsAdmin = false;
            WorkingDirectoryMode = WorkingDirectoryMode.ScriptDirectory;
            CustomWorkingDirectory = "";
            Arguments = "";
            ValidationError = "";

            try
            {
                CodeContent = await _scriptService.LoadScriptContentAsync(importedFilePath);
            }
            catch
            {
                CodeContent = "";
            }
        }
        else
        {
            Title = "Новый скрипт";
            Description = "";
            FilePath = "";
            ScriptType = defaultType;
            SelectedCategoryId = Categories.FirstOrDefault()?.Id ?? "";
            Icon = "DocumentCode24";
            AccentColor = defaultType == ScriptType.PowerShell ? "#0078D4" : "#D83B01";
            TileSize = TileSize.Standard;
            RunAsAdmin = false;
            WorkingDirectoryMode = WorkingDirectoryMode.ScriptDirectory;
            CustomWorkingDirectory = "";
            Arguments = "";
            ValidationError = "";

            CodeContent = defaultType == ScriptType.PowerShell
                ? "# Напишите ваш PowerShell скрипт здесь\nWrite-Host 'Hello from Script Hub!'"
                : "@echo off\necho Hello from Script Hub!\npause";
        }
    }

    private void RefreshCategories()
    {
        Categories.Clear();
        foreach (var cat in _scriptService.GetAllCategories())
        {
            Categories.Add(cat);
        }

        if (string.IsNullOrEmpty(SelectedCategoryId) && Categories.Count > 0)
        {
            SelectedCategoryId = Categories[0].Id;
        }
    }

    private async void BrowseScriptFile()
    {
        var file = _dialogService.PickScriptFile();
        if (!string.IsNullOrWhiteSpace(file))
        {
            FilePath = file;
            if (string.IsNullOrWhiteSpace(Title) || Title == "Новый скрипт")
            {
                Title = Path.GetFileNameWithoutExtension(file);
            }

            var ext = Path.GetExtension(file).ToLowerInvariant();
            ScriptType = ext switch
            {
                ".bat" => ScriptType.Batch,
                ".cmd" => ScriptType.Cmd,
                _ => ScriptType.PowerShell
            };

            if (File.Exists(file))
            {
                try
                {
                    CodeContent = await _scriptService.LoadScriptContentAsync(file);
                }
                catch { }
            }
        }
    }

    private void BrowseCustomWorkDir()
    {
        var folder = _dialogService.PickFolder(CustomWorkingDirectory);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            CustomWorkingDirectory = folder;
        }
    }

    private async Task SaveAsync(bool thenRun)
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            ValidationError = "Пожалуйста, введите название скрипта.";
            return;
        }

        ValidationError = "";

        // Enforce saving and duplicating strictly into the \Scripts folder
        var savedPath = await _scriptService.EnsureScriptInScriptsFolderAsync(FilePath, Title, ScriptType, CodeContent);
        FilePath = savedPath;

        _editingScript.Title = Title.Trim();
        _editingScript.Description = Description?.Trim() ?? "";
        _editingScript.FilePath = FilePath;
        _editingScript.ScriptType = ScriptType;
        _editingScript.CategoryId = SelectedCategoryId;
        _editingScript.Icon = Icon;
        _editingScript.AccentColor = AccentColor;
        _editingScript.TileSize = TileSize;
        _editingScript.RunAsAdmin = RunAsAdmin;
        _editingScript.WorkingDirectoryMode = WorkingDirectoryMode;
        _editingScript.CustomWorkingDirectory = CustomWorkingDirectory?.Trim() ?? "";
        _editingScript.Arguments = Arguments?.Trim() ?? "";

        await _scriptService.SaveScriptAsync(_editingScript, CodeContent);

        _onSaveCompleted(_editingScript, thenRun);
    }
}
