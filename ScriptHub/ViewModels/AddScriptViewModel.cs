using System;
using System.IO;
using System.Windows.Input;
using ScriptHub.Helpers;
using ScriptHub.Models;
using ScriptHub.Services.Contracts;

namespace ScriptHub.ViewModels;

public class AddScriptViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;
    private readonly Action<ScriptType, string?> _onOptionSelected;
    private readonly Action _onCancel;

    public ICommand ImportExistingCommand { get; }
    public ICommand CreateNewPowerShellCommand { get; }
    public ICommand CreateNewBatchCommand { get; }
    public ICommand CancelCommand { get; }

    public AddScriptViewModel(
        IDialogService dialogService,
        Action<ScriptType, string?> onOptionSelected,
        Action onCancel)
    {
        _dialogService = dialogService;
        _onOptionSelected = onOptionSelected;
        _onCancel = onCancel;

        ImportExistingCommand = new RelayCommand(ImportExisting);
        CreateNewPowerShellCommand = new RelayCommand(() => _onOptionSelected(ScriptType.PowerShell, null));
        CreateNewBatchCommand = new RelayCommand(() => _onOptionSelected(ScriptType.Batch, null));
        CancelCommand = new RelayCommand(_onCancel);
    }

    private void ImportExisting()
    {
        var file = _dialogService.PickScriptFile();
        if (!string.IsNullOrWhiteSpace(file) && File.Exists(file))
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            var scriptType = ext switch
            {
                ".bat" => ScriptType.Batch,
                ".cmd" => ScriptType.Cmd,
                _ => ScriptType.PowerShell
            };

            _onOptionSelected(scriptType, file);
        }
    }
}
