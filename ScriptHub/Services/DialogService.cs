using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using ScriptHub.Services.Contracts;
using ScriptHub.Views;

namespace ScriptHub.Services;

public class DialogService : IDialogService
{
    public async Task ShowMessageAsync(string title, string message)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            System.Windows.MessageBox.Show(
                Application.Current.MainWindow,
                message,
                title,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        });
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            System.Windows.MessageBox.Show(
                Application.Current.MainWindow,
                message,
                title,
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        });
    }

    public async Task<bool> ShowConfirmationAsync(string title, string message, string primaryText = "Да", string secondaryText = "Отмена")
    {
        return await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var res = System.Windows.MessageBox.Show(
                Application.Current.MainWindow,
                message,
                title,
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            return res == System.Windows.MessageBoxResult.Yes;
        });
    }

    public async Task<(bool Confirmed, bool DeletePhysicalFile)> ShowDeleteConfirmationAsync(string scriptTitle)
    {
        return await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dlg = new ConfirmDeleteDialog(scriptTitle)
            {
                Owner = Application.Current.MainWindow
            };
            var res = dlg.ShowDialog();
            if (res == true)
            {
                return (true, dlg.DeletePhysicalFile);
            }
            return (false, false);
        });
    }

    public string? PickScriptFile()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Выберите файл скрипта",
            Filter = "Все скрипты (*.ps1;*.bat;*.cmd)|*.ps1;*.bat;*.cmd|PowerShell (*.ps1)|*.ps1|Batch / CMD (*.bat;*.cmd)|*.bat;*.cmd|Все файлы (*.*)|*.*",
            Multiselect = false
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }

    public string? PickFolder(string? defaultPath = null)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Выберите рабочую папку",
            InitialDirectory = !string.IsNullOrWhiteSpace(defaultPath) && System.IO.Directory.Exists(defaultPath)
                ? defaultPath
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        return dlg.ShowDialog() == true ? dlg.FolderName : null;
    }

    public string? PickSaveFile(string defaultFileName, string filter)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Сохранить файл",
            FileName = defaultFileName,
            Filter = filter
        };

        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
