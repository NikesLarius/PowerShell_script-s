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

    Task<ExecutionResult> ExecuteCommandAsync(
        string command,
        ScriptType scriptType,
        string workingDirectory,
        bool runAsAdmin,
        Action<string, bool> outputCallback,
        CancellationToken cancellationToken = default);
        
    void SendInput(string input);
    void StopCurrentProcess();
    
    string ResolveWorkingDirectory(ScriptModel script);
    string GetPowerShellPath(bool preferPwsh7 = false, string? customPath = null);
}
