using System;
using System.Threading.Tasks;

namespace ScriptHub.Services.Contracts;

public class UpdateResult
{
    public bool Success { get; set; }
    public bool HasUpdates { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

public interface IUpdateService
{
    Task<UpdateResult> CheckForUpdatesAsync();
    Task<UpdateResult> UpdateFromGitHubAsync(IProgress<string>? progress = null);
    void OpenGitHubRepository();
}
