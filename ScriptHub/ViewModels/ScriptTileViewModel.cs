using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ScriptHub.Helpers;
using ScriptHub.Models;
using ScriptHub.Services.Contracts;

namespace ScriptHub.ViewModels;

public class ScriptTileViewModel : ViewModelBase
{
    private readonly ScriptModel _model;
    private readonly IScriptService _scriptService;
    private readonly Action<ScriptTileViewModel> _onRun;
    private readonly Action<ScriptTileViewModel> _onEdit;
    private readonly Action<ScriptTileViewModel> _onDelete;
    private readonly Action<ScriptTileViewModel> _onFavoriteChanged;
    private string _categoryName = "";
    private string _categoryColor = "#0078D4";
    private bool _isFileMissing;
    private bool _isExpanded;
    private bool _isPinnedFolder;

    public ScriptModel Model => _model;

    public string Id => _model.Id;
    public string Title => _model.Title;
    public string Description => _model.Description;
    public string FilePath => _model.FilePath;
    public ScriptType ScriptType => _model.ScriptType;
    public string CategoryId => _model.CategoryId;
    public string Icon => _model.Icon;
    public string AccentColor => _model.AccentColor;
    public TileSize TileSize => _model.TileSize;
    public bool RunAsAdmin => _model.RunAsAdmin;
    public int OrderIndex => _model.OrderIndex;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public string CustomWorkingDirectory
    {
        get => _model.CustomWorkingDirectory;
        set
        {
            var val = value?.Trim() ?? "";
            if (_model.CustomWorkingDirectory != val)
            {
                _model.CustomWorkingDirectory = val;
                _model.WorkingDirectoryMode = string.IsNullOrWhiteSpace(val) ? WorkingDirectoryMode.ScriptDirectory : WorkingDirectoryMode.CustomDirectory;

                if (_isPinnedFolder)
                {
                    _ = _scriptService.SaveScriptAsync(_model);
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayWorkingDirectory));
            }
        }
    }

    public bool IsPinnedCustomDirectory
    {
        get => _isPinnedFolder;
        set
        {
            if (SetProperty(ref _isPinnedFolder, value))
            {
                _model.WorkingDirectoryMode = value ? WorkingDirectoryMode.CustomDirectory : WorkingDirectoryMode.ScriptDirectory;
                _ = _scriptService.SaveScriptAsync(_model);
                OnPropertyChanged(nameof(DisplayWorkingDirectory));
            }
        }
    }

    public string DisplayWorkingDirectory
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_model.CustomWorkingDirectory))
            {
                return _model.CustomWorkingDirectory;
            }
            if (!string.IsNullOrWhiteSpace(_model.FilePath))
            {
                return Path.GetDirectoryName(_model.FilePath) ?? "Папка скрипта";
            }
            return "Папка скрипта";
        }
    }

    public bool IsFavorite
    {
        get => _model.IsFavorite;
        set
        {
            if (_model.IsFavorite != value)
            {
                _model.IsFavorite = value;
                OnPropertyChanged();
                _onFavoriteChanged(this);
            }
        }
    }

    public ExecutionStatus LastStatus
    {
        get => _model.LastStatus;
        set
        {
            if (_model.LastStatus != value)
            {
                _model.LastStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastStatusFormatted));
            }
        }
    }

    public DateTime? LastRunAt
    {
        get => _model.LastRunAt;
        set
        {
            if (_model.LastRunAt != value)
            {
                _model.LastRunAt = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastRunAtFormatted));
            }
        }
    }

    public TimeSpan? LastDuration
    {
        get => _model.LastDuration;
        set
        {
            if (_model.LastDuration != value)
            {
                _model.LastDuration = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastDurationFormatted));
            }
        }
    }

    public string LastRunAtFormatted => LastRunAt.HasValue 
        ? LastRunAt.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm") 
        : "Никогда";

    public string LastDurationFormatted => LastDuration.HasValue 
        ? $"{LastDuration.Value.TotalSeconds:F1} сек." 
        : "";

    public string LastStatusFormatted => LastStatus switch
    {
        ExecutionStatus.Running => "Выполняется...",
        ExecutionStatus.Success => "Успешно",
        ExecutionStatus.Failed => $"Ошибка ({_model.LastExitCode})",
        ExecutionStatus.Cancelled => "Остановлен",
        _ => "Не запускался"
    };

    public string CategoryName
    {
        get => _categoryName;
        set => SetProperty(ref _categoryName, value);
    }

    public string CategoryColor
    {
        get => _categoryColor;
        set => SetProperty(ref _categoryColor, value);
    }

    public bool IsFileMissing
    {
        get => _isFileMissing;
        set => SetProperty(ref _isFileMissing, value);
    }

    public ICommand RunCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand ToggleFavoriteCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand CopyPathCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand ToggleExpandedCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand SetPresetDownloadsCommand { get; }
    public ICommand SetPresetDesktopCommand { get; }
    public ICommand SetPresetScriptDirCommand { get; }
    public ICommand RelinkFileCommand { get; }

    public ScriptTileViewModel(
        ScriptModel model,
        IScriptService scriptService,
        Action<ScriptTileViewModel> onRun,
        Action<ScriptTileViewModel> onEdit,
        Action<ScriptTileViewModel> onDelete,
        Action<ScriptTileViewModel> onFavoriteChanged)
    {
        _model = model;
        _scriptService = scriptService;
        _onRun = onRun;
        _onEdit = onEdit;
        _onDelete = onDelete;
        _onFavoriteChanged = onFavoriteChanged;

        _isPinnedFolder = model.WorkingDirectoryMode == WorkingDirectoryMode.CustomDirectory && !string.IsNullOrWhiteSpace(model.CustomWorkingDirectory);

        RunCommand = new RelayCommand(() => _onRun(this));
        EditCommand = new RelayCommand(() => _onEdit(this));
        ToggleFavoriteCommand = new RelayCommand(() => IsFavorite = !IsFavorite);
        OpenFolderCommand = new RelayCommand(OpenContainingFolder);
        OpenFileCommand = new RelayCommand(OpenScriptInEditor);
        CopyPathCommand = new RelayCommand(CopyFilePath);
        DeleteCommand = new RelayCommand(() => _onDelete(this));

        ToggleExpandedCommand = new RelayCommand(() => IsExpanded = !IsExpanded);
        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        RelinkFileCommand = new RelayCommand(RelinkFile);
        
        SetPresetDownloadsCommand = new RelayCommand(() =>
        {
            CustomWorkingDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        });
        
        SetPresetDesktopCommand = new RelayCommand(() =>
        {
            CustomWorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        });
        
        SetPresetScriptDirCommand = new RelayCommand(() =>
        {
            CustomWorkingDirectory = "";
        });

        RefreshFileStatus();
    }

    private void BrowseFolder()
    {
        var current = !string.IsNullOrWhiteSpace(CustomWorkingDirectory) && Directory.Exists(CustomWorkingDirectory)
            ? CustomWorkingDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var dlg = new OpenFolderDialog
        {
            Title = "Выберите рабочую папку для запуска скрипта",
            InitialDirectory = current
        };

        if (dlg.ShowDialog() == true)
        {
            CustomWorkingDirectory = dlg.FolderName;
        }
    }

    private void RelinkFile()
    {
        var filter = _model.ScriptType switch
        {
            ScriptType.PowerShell => "PowerShell скрипты (*.ps1)|*.ps1|Все файлы (*.*)|*.*",
            ScriptType.Batch or ScriptType.Cmd => "Командные файлы (*.bat;*.cmd)|*.bat;*.cmd|Все файлы (*.*)|*.*",
            _ => "Файлы скриптов (*.ps1;*.bat;*.cmd)|*.ps1;*.bat;*.cmd|Все файлы (*.*)|*.*"
        };

        var currentDir = !string.IsNullOrWhiteSpace(_model.FilePath)
            ? Path.GetDirectoryName(_model.FilePath)
            : null;

        var initialDir = !string.IsNullOrWhiteSpace(currentDir) && Directory.Exists(currentDir)
            ? currentDir
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        var dlg = new OpenFileDialog
        {
            Title = $"Указать файл для скрипта: {_model.Title}",
            Filter = filter,
            InitialDirectory = initialDir,
            FileName = !string.IsNullOrWhiteSpace(_model.FilePath) ? Path.GetFileName(_model.FilePath) : ""
        };

        if (dlg.ShowDialog() == true && File.Exists(dlg.FileName))
        {
            _model.FilePath = dlg.FileName;
            RefreshFileStatus();
            _ = _scriptService.SaveScriptAsync(_model);
            RefreshProperties();
        }
    }

    public void RefreshFileStatus()
    {
        if (!string.IsNullOrWhiteSpace(_model.FilePath) && !File.Exists(_model.FilePath))
        {
            var resolved = _scriptService.ResolveScriptPath(_model.FilePath, _model.ScriptType);
            if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved) && !string.Equals(_model.FilePath, resolved, StringComparison.OrdinalIgnoreCase))
            {
                _model.FilePath = resolved;
                _ = _scriptService.SaveScriptAsync(_model);
                OnPropertyChanged(nameof(FilePath));
            }
        }

        IsFileMissing = !string.IsNullOrWhiteSpace(_model.FilePath) && !File.Exists(_model.FilePath);
    }

    public void RefreshProperties()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(FilePath));
        OnPropertyChanged(nameof(ScriptType));
        OnPropertyChanged(nameof(CategoryId));
        OnPropertyChanged(nameof(Icon));
        OnPropertyChanged(nameof(AccentColor));
        OnPropertyChanged(nameof(TileSize));
        OnPropertyChanged(nameof(RunAsAdmin));
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(LastStatus));
        OnPropertyChanged(nameof(LastStatusFormatted));
        OnPropertyChanged(nameof(LastRunAtFormatted));
        OnPropertyChanged(nameof(LastDurationFormatted));
        OnPropertyChanged(nameof(CustomWorkingDirectory));
        OnPropertyChanged(nameof(IsPinnedCustomDirectory));
        OnPropertyChanged(nameof(DisplayWorkingDirectory));
        RefreshFileStatus();
    }

    private void OpenContainingFolder()
    {
        try
        {
            if (File.Exists(_model.FilePath))
            {
                Process.Start("explorer.exe", $"/select,\"{_model.FilePath}\"");
            }
            else if (!string.IsNullOrWhiteSpace(_model.FilePath))
            {
                var dir = Path.GetDirectoryName(_model.FilePath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    Process.Start("explorer.exe", dir);
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть папку: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenScriptInEditor()
    {
        try
        {
            if (File.Exists(_model.FilePath))
            {
                Process.Start(new ProcessStartInfo(_model.FilePath) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось открыть файл: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CopyFilePath()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_model.FilePath))
            {
                Clipboard.SetText(_model.FilePath);
            }
        }
        catch { }
    }
}
