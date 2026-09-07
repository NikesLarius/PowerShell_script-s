using System;
using System.Threading;
using System.Threading.Tasks;
using ScriptHub.Models;

namespace ScriptHub.Services.Contracts;

public interface IProcessService
{
    bool IsRunning { get; }
    ScriptModel? CurrentlyRunningScript { get; }
    
    event EventHandler<bool>? ExecutionStateChanged;
    
    Task<ExecutionResult> ExecuteScriptAsync(
        ScriptModel script, 
        Action<string, bool> outputCallback, 
        CancellationToken cancellationToken = default);
        
    void StopCurrentProcess();
    
    string ResolveWorkingDirectory(ScriptModel script);
    string GetPowerShellPath(bool preferPwsh7 = false, string? customPath = null);
}
