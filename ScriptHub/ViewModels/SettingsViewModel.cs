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

public class SettingsViewModel : ViewModelBase
{
    private readonly IScriptService _scriptService;
    private readonly IBackupService _backupService;
    private readonly IDialogService _dialogService;
    private readonly IUpdateService _updateService;
    private readonly Action<ThemeMode> _onThemeChanged;
    private readonly Action<double> _onOpacityChanged;
    private readonly Action _onDataReloadNeeded;

    private AppConfigModel _config = new();
    private string _newCategoryName = "";
    private string _newCategoryColor = "#0078D4";
    private string _backupStatusMessage = "";
    private string _updateStatusMessage = "";
    private bool _isUpdating;

    public ThemeMode SelectedTheme
    {
        get => _config.Theme;
        set
        {
            if (_config.Theme != value)
            {
                _config.Theme = value;
                OnPropertyChanged();
                _onThemeChanged(value);
                SaveConfig();
            }
        }
    }

    public double WindowOpacity
    {
        get => _config.WindowOpacity <= 0 ? 1.0 : _config.WindowOpacity;
        set
        {
            var clamped = Math.Clamp(value, 0.2, 1.0);
            if (Math.Abs(_config.WindowOpacity - clamped) > 0.001)
            {
                _config.WindowOpacity = clamped;
                OnPropertyChanged();
                OnPropertyChanged(nameof(WindowOpacityPercent));
                _onOpacityChanged?.Invoke(clamped);
                SaveConfig();
            }
        }
    }

    public int WindowOpacityPercent
    {
        get => (int)Math.Round(WindowOpacity * 100);
        set
        {
            var frac = Math.Clamp(value, 20, 100) / 100.0;
            WindowOpacity = frac;
        }
    }

    public TileSize DefaultTileSize
    {
        get => _config.DefaultTileSize;
        set
        {
            if (_config.DefaultTileSize != value)
            {
                _config.DefaultTileSize = value;
                OnPropertyChanged();
                SaveConfig();
            }
        }
    }

    public ScriptSortOrder DefaultSortOrder
    {
        get => _config.DefaultSortOrder;
        set
        {
            if (_config.DefaultSortOrder != value)
            {
                _config.DefaultSortOrder = value;
                OnPropertyChanged();
                SaveConfig();
            }
        }
    }

    public bool PreferPowerShell7
    {
        get => _config.PreferPowerShell7;
        set
        {
            if (_config.PreferPowerShell7 != value)
            {
                _config.PreferPowerShell7 = value;
                OnPropertyChanged();
                SaveConfig();
            }
        }
    }

    public bool ConfirmBeforeDeletion
    {
        get => _config.ConfirmBeforeDeletion;
        set
        {
            if (_config.ConfirmBeforeDeletion != value)
            {
                _config.ConfirmBeforeDeletion = value;
                OnPropertyChanged();
                SaveConfig();
            }
        }
    }

    public string DefaultWorkingDirectory
    {
        get => _config.DefaultWorkingDirectory;
        set
        {
            if (_config.DefaultWorkingDirectory != value)
            {
                _config.DefaultWorkingDirectory = value;
                OnPropertyChanged();
                SaveConfig();
            }
        }
    }

    public string NewCategoryName
    {
        get => _newCategoryName;
        set => SetProperty(ref _newCategoryName, value);
    }

    public string NewCategoryColor
    {
        get => _newCategoryColor;
        set => SetProperty(ref _newCategoryColor, value);
    }

    public string BackupStatusMessage
    {
        get => _backupStatusMessage;
        set => SetProperty(ref _backupStatusMessage, value);
    }

    public string UpdateStatusMessage
    {
        get => _updateStatusMessage;
        set => SetProperty(ref _updateStatusMessage, value);
    }

    public bool IsUpdating
    {
        get => _isUpdating;
        set => SetProperty(ref _isUpdating, value);
    }

    public ObservableCollection<CategoryModel> Categories { get; } = new();

    public ICommand ExportBackupCommand { get; }
    public ICommand ImportBackupCommand { get; }
    public ICommand AddCategoryCommand { get; }
    public ICommand DeleteCategoryCommand { get; }
    public ICommand BrowseDefaultWorkDirCommand { get; }
    public ICommand UpdateFromGitHubCommand { get; }
    public ICommand OpenGitHubRepoCommand { get; }

    public SettingsViewModel(
        IScriptService scriptService,
        IBackupService backupService,
        IDialogService dialogService,
        IUpdateService updateService,
        Action<ThemeMode> onThemeChanged,
        Action<double> onOpacityChanged,
        Action onDataReloadNeeded)
    {
        _scriptService = scriptService;
        _backupService = backupService;
        _dialogService = dialogService;
        _updateService = updateService;
        _onThemeChanged = onThemeChanged;
        _onOpacityChanged = onOpacityChanged;
        _onDataReloadNeeded = onDataReloadNeeded;

        ExportBackupCommand = new AsyncRelayCommand(ExportBackupAsync);
        ImportBackupCommand = new AsyncRelayCommand(ImportBackupAsync);
        AddCategoryCommand = new AsyncRelayCommand(AddCategoryAsync);
        DeleteCategoryCommand = new RelayCommand<CategoryModel>(async cat => await DeleteCategoryAsync(cat));
        BrowseDefaultWorkDirCommand = new RelayCommand(BrowseDefaultWorkDir);
        UpdateFromGitHubCommand = new AsyncRelayCommand(UpdateFromGitHubAsync);
        OpenGitHubRepoCommand = new RelayCommand(OpenGitHubRepo);

        LoadSettings();
    }

    public void LoadSettings()
    {
        _config = _scriptService.GetConfig();
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(WindowOpacity));
        OnPropertyChanged(nameof(WindowOpacityPercent));
        OnPropertyChanged(nameof(DefaultTileSize));
        OnPropertyChanged(nameof(DefaultSortOrder));
        OnPropertyChanged(nameof(PreferPowerShell7));
        OnPropertyChanged(nameof(ConfirmBeforeDeletion));
        OnPropertyChanged(nameof(DefaultWorkingDirectory));

        Categories.Clear();
        foreach (var cat in _scriptService.GetAllCategories())
        {
            Categories.Add(cat);
        }
    }

    private void SaveConfig()
    {
        _ = _scriptService.UpdateConfigAsync(_config);
    }

    private void BrowseDefaultWorkDir()
    {
        var folder = _dialogService.PickFolder(DefaultWorkingDirectory);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            DefaultWorkingDirectory = folder;
        }
    }

    private async Task AddCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName)) return;

        var category = new CategoryModel
        {
            Id = "cat-" + Guid.NewGuid().ToString("N")[..8],
            Name = NewCategoryName.Trim(),
            ColorHex = string.IsNullOrWhiteSpace(NewCategoryColor) ? "#0078D4" : NewCategoryColor,
            Icon = "Folder24",
            IsSystem = false
        };

        await _scriptService.AddCategoryAsync(category);
        Categories.Add(category);
        NewCategoryName = "";
    }

    private async Task DeleteCategoryAsync(CategoryModel? category)
    {
        if (category == null || category.IsSystem) return;

        var confirm = await _dialogService.ShowConfirmationAsync(
            "Удаление категории", 
            $"Вы уверены, что хотите удалить категорию \"{category.Name}\"?");

        if (confirm)
        {
            await _scriptService.DeleteCategoryAsync(category.Id);
            Categories.Remove(category);
        }
    }

    private async Task ExportBackupAsync()
    {
        var targetFile = _dialogService.PickSaveFile(
            $"ScriptHub_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip", 
            "Резервная копия Script Hub (*.zip)|*.zip");

        if (string.IsNullOrWhiteSpace(targetFile)) return;

        try
        {
            BackupStatusMessage = "Экспорт данных...";
            await _backupService.ExportBackupAsync(targetFile);
            BackupStatusMessage = $"Резервная копия успешно создана:\n{targetFile}";
            await _dialogService.ShowMessageAsync("Экспорт конфигурации", "Резервная копия успешно создана!");
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Ошибка экспорта: {ex.Message}";
            await _dialogService.ShowErrorAsync("Ошибка экспорта", ex.Message);
        }
    }

    private async Task ImportBackupAsync()
    {
        var sourceFile = _dialogService.PickScriptFile();
        if (string.IsNullOrWhiteSpace(sourceFile)) return;

        var confirm = await _dialogService.ShowConfirmationAsync(
            "Импорт конфигурации", 
            "Внимание: Импорт заменит текущие настройки и скрипты резервной копией. Продолжить?");

        if (!confirm) return;

        try
        {
            BackupStatusMessage = "Импорт данных...";
            var success = await _backupService.ImportBackupAsync(sourceFile);
            if (success)
            {
                BackupStatusMessage = "Конфигурация успешно восстановлена!";
                await _dialogService.ShowMessageAsync("Импорт завершен", "Конфигурация успешно восстановлена.");
                _onDataReloadNeeded();
                LoadSettings();
            }
            else
            {
                BackupStatusMessage = "Не удалось восстановить данные из указанного файла.";
                await _dialogService.ShowErrorAsync("Ошибка импорта", "Не удалось восстановить данные.");
            }
        }
        catch (Exception ex)
        {
            BackupStatusMessage = $"Ошибка импорта: {ex.Message}";
            await _dialogService.ShowErrorAsync("Ошибка импорта", ex.Message);
        }
    }

    public async Task UpdateFromGitHubAsync()
    {
        if (IsUpdating) return;
        IsUpdating = true;
        UpdateStatusMessage = "Проверка и загрузка обновлений с GitHub...";

        try
        {
            var progress = new Progress<string>(msg => UpdateStatusMessage = msg);
            var result = await _updateService.UpdateFromGitHubAsync(progress);
            UpdateStatusMessage = result.Message;

            if (result.Success)
            {
                _onDataReloadNeeded();
                LoadSettings();
                await _dialogService.ShowMessageAsync("Обновление с GitHub", result.Message + (string.IsNullOrWhiteSpace(result.Details) ? "" : $"\n\n{result.Details}"));
            }
            else
            {
                await _dialogService.ShowErrorAsync("Ошибка обновления", result.Message);
            }
        }
        catch (Exception ex)
        {
            UpdateStatusMessage = $"Ошибка: {ex.Message}";
            await _dialogService.ShowErrorAsync("Ошибка обновления", ex.Message);
        }
        finally
        {
            IsUpdating = false;
        }
    }

    private void OpenGitHubRepo()
    {
        _updateService.OpenGitHubRepository();
    }
}
