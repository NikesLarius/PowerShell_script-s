using System.Threading.Tasks;

namespace ScriptHub.Services.Contracts;

public interface IDialogService
{
    Task ShowMessageAsync(string title, string message);
    Task ShowErrorAsync(string title, string message);
    Task<bool> ShowConfirmationAsync(string title, string message, string primaryText = "Да", string secondaryText = "Отмена");
    Task<(bool Confirmed, bool DeletePhysicalFile)> ShowDeleteConfirmationAsync(string scriptTitle);
    
    string? PickScriptFile();
    string? PickZipFile();
    string? PickFolder(string? defaultPath = null);
    string? PickSaveFile(string defaultFileName, string filter);
}
