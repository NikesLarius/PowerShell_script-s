using System.Threading.Tasks;

namespace ScriptHub.Services.Contracts;

public interface IBackupService
{
    Task<string> ExportBackupAsync(string targetZipPath);
    Task<bool> ImportBackupAsync(string sourceZipPath);
}
