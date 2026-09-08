using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ScriptHub.Helpers;
using ScriptHub.Models;
using ScriptHub.Services.Contracts;

namespace ScriptHub.Services;

public class ProcessService : IProcessService
{
    private Process? _currentProcess;
    private readonly object _lock = new();

    public bool IsRunning => _currentProcess is { HasExited: false };
    public ScriptModel? CurrentlyRunningScript { get; private set; }

    public event EventHandler<bool>? ExecutionStateChanged;

    public string GetPowerShellPath(bool preferPwsh7 = false, string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
        {
            return customPath;
        }

        if (preferPwsh7)
        {
            var pwshPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "PowerShell", "7", "pwsh.exe");
            if (File.Exists(pwshPath)) return pwshPath;

            var localPwsh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "PowerShell", "pwsh.exe");
            if (File.Exists(localPwsh)) return localPwsh;
        }

        return "powershell.exe";
    }

    public string ResolveWorkingDirectory(ScriptModel script)
    {
        try
        {
            // 1. If custom working directory is specified, prioritize it
            if (!string.IsNullOrWhiteSpace(script.CustomWorkingDirectory))
            {
                var customDir = script.CustomWorkingDirectory.Trim().Trim('"', '\'');
                if (!string.IsNullOrWhiteSpace(customDir))
                {
                    if (!Directory.Exists(customDir))
                    {
                        try { Directory.CreateDirectory(customDir); } catch { }
                    }
                    if (Directory.Exists(customDir))
                    {
                        return customDir;
                    }
                    return customDir;
                }
            }

            // 2. Fallback to script folder if exists
            if (!string.IsNullOrWhiteSpace(script.FilePath) && File.Exists(script.FilePath))
            {
                var dir = Path.GetDirectoryName(script.FilePath);
                if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                {
                    return dir;
                }
            }

            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }
        catch
        {
            return Environment.CurrentDirectory;
        }
    }

    public async Task<ExecutionResult> ExecuteScriptAsync(
        ScriptModel script, 
        Action<string, bool> outputCallback, 
        CancellationToken cancellationToken = default)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("Другой скрипт уже выполняется. Дождитесь завершения или остановите его.");
        }

        var result = new ExecutionResult
        {
            StartTime = DateTime.UtcNow,
            Status = ExecutionStatus.Running
        };

        CurrentlyRunningScript = script;
        ExecutionStateChanged?.Invoke(this, true);

        var stopwatch = Stopwatch.StartNew();
        var fullOutput = new StringBuilder();

        void AppendOutput(string text, bool isError)
        {
            fullOutput.AppendLine(text);
            outputCallback(text, isError);
        }

        var workDir = ResolveWorkingDirectory(script);
        var appVersion = typeof(ProcessService).Assembly.GetName().Version;
        var versionStr = appVersion != null ? $"{appVersion.Major}.{appVersion.Minor}" : "1.4";
        AppendOutput($"> Script Hub v{versionStr}", false);
        AppendOutput($"> Запуск: {script.Title}", false);
        AppendOutput($"> Файл: {script.FilePath}", false);
        AppendOutput($"> Рабочая папка: {workDir}", false);
        if (!string.IsNullOrWhiteSpace(script.Arguments))
        {
            AppendOutput($"> Аргументы: {script.Arguments}", false);
        }
        AppendOutput($"> Запуск от администратора: {(script.RunAsAdmin ? "Да" : "Нет")}", false);
        AppendOutput(new string('-', 50), false);

        if (!File.Exists(script.FilePath))
        {
            var err = $"Ошибка: Файл скрипта не найден: {script.FilePath}";
            AppendOutput(err, true);
            stopwatch.Stop();
            result.EndTime = DateTime.UtcNow;
            result.Duration = stopwatch.Elapsed;
            result.ExitCode = -1;
            result.Status = ExecutionStatus.Failed;
            result.ErrorMessage = err;
            result.Output = fullOutput.ToString();

            CurrentlyRunningScript = null;
            ExecutionStateChanged?.Invoke(this, false);
            return result;
        }

        if (script.ScriptType == ScriptType.PowerShell)
        {
            EncodingHelper.EnsurePs1FileEncoding(script.FilePath);
        }

        string? tempRunnerFile = null;
        string? tempLogFile = null;

        try
        {
            var psi = new ProcessStartInfo();
            psi.WorkingDirectory = workDir;
            var escapedWorkDir = workDir.Replace("'", "''");
            var escapedScriptPath = script.FilePath.Replace("'", "''");
            var argsStr = string.IsNullOrWhiteSpace(script.Arguments) ? "" : " " + script.Arguments;

            if (script.RunAsAdmin)
            {
                tempLogFile = Path.Combine(Path.GetTempPath(), $"scripthub_log_{Guid.NewGuid():N}.txt");
                var escapedTempLog = tempLogFile.Replace("'", "''");

                psi.Verb = "runas";
                psi.UseShellExecute = true;
                psi.CreateNoWindow = false;

                if (script.ScriptType == ScriptType.PowerShell)
                {
                    tempRunnerFile = Path.Combine(Path.GetTempPath(), $"scripthub_runner_{Guid.NewGuid():N}.ps1");

                    var runnerCode = $@"
[Console]::InputEncoding = [Console]::OutputEncoding = $OutputEncoding = [System.Text.Encoding]::UTF8
Set-Location -LiteralPath '{escapedWorkDir}'
[System.IO.Directory]::SetCurrentDirectory('{escapedWorkDir}')
$PSScriptRoot = '{escapedWorkDir}'
$global:PSScriptRoot = '{escapedWorkDir}'
$PSCommandPath = '{escapedScriptPath}'

try {{
    . '{escapedScriptPath}'{argsStr} *>&1 | Out-File -FilePath '{escapedTempLog}' -Encoding utf8
}}
catch {{
    $_ | Out-File -FilePath '{escapedTempLog}' -Append -Encoding utf8
    exit 1
}}
";
                    await EncodingHelper.WriteTextUtf8BomAsync(tempRunnerFile, runnerCode);

                    psi.FileName = GetPowerShellPath(false, script.CustomInterpreterPath);
                    psi.Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempRunnerFile}\"";
                }
                else // Batch / CMD
                {
                    psi.FileName = "cmd.exe";
                    psi.Arguments = $"/c \"cd /d \"{workDir}\" && chcp 65001 >nul && \"{script.FilePath}\"{argsStr} > \"{tempLogFile}\" 2>&1\"";
                }

                AppendOutput("> Запрос прав администратора (UAC)...", false);
                
                using var proc = new Process { StartInfo = psi };
                lock (_lock)
                {
                    _currentProcess = proc;
                }

                proc.Start();
                AppendOutput("> Процесс запущен с правами администратора.", false);

                // Read logs in real-time
                using (var fileStream = new FileStream(tempLogFile, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(fileStream, Encoding.UTF8))
                {
                    while (!proc.HasExited)
                    {
                        while (reader.ReadLine() is { } line)
                        {
                            var isErr = line.Contains("error", StringComparison.OrdinalIgnoreCase) || 
                                        line.Contains("ошибка", StringComparison.OrdinalIgnoreCase) ||
                                        line.StartsWith("Exception:", StringComparison.OrdinalIgnoreCase) ||
                                        line.StartsWith("ПРЕДУПРЕЖДЕНИЕ:", StringComparison.OrdinalIgnoreCase);
                            AppendOutput(line, isErr);
                        }
                        await Task.Delay(150, cancellationToken);
                    }

                    // Remaining lines
                    while (reader.ReadLine() is { } line)
                    {
                        var isErr = line.Contains("error", StringComparison.OrdinalIgnoreCase) || 
                                    line.Contains("ошибка", StringComparison.OrdinalIgnoreCase) ||
                                    line.StartsWith("Exception:", StringComparison.OrdinalIgnoreCase) ||
                                    line.StartsWith("ПРЕДУПРЕЖДЕНИЕ:", StringComparison.OrdinalIgnoreCase);
                        AppendOutput(line, isErr);
                    }
                }

                stopwatch.Stop();
                result.EndTime = DateTime.UtcNow;
                result.Duration = stopwatch.Elapsed;
                result.ExitCode = proc.ExitCode;
                result.Status = proc.ExitCode == 0 ? ExecutionStatus.Success : ExecutionStatus.Failed;

                AppendOutput(new string('-', 50), false);
                if (proc.ExitCode != 0)
                {
                    AppendOutput($"> Процесс завершился с ошибкой (код {proc.ExitCode}). Длительность: {stopwatch.Elapsed.TotalSeconds:F2} сек.", true);
                }
                else
                {
                    AppendOutput($"> Процесс успешно завершен (код 0). Длительность: {stopwatch.Elapsed.TotalSeconds:F2} сек.", false);
                }
            }
            else
            {
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                psi.StandardOutputEncoding = Encoding.UTF8;
                psi.StandardErrorEncoding = Encoding.UTF8;

                if (script.ScriptType == ScriptType.PowerShell)
                {
                    tempRunnerFile = Path.Combine(Path.GetTempPath(), $"scripthub_runner_{Guid.NewGuid():N}.ps1");

                    var runnerCode = $@"
[Console]::InputEncoding = [Console]::OutputEncoding = $OutputEncoding = [System.Text.Encoding]::UTF8
Set-Location -LiteralPath '{escapedWorkDir}'
[System.IO.Directory]::SetCurrentDirectory('{escapedWorkDir}')
$PSScriptRoot = '{escapedWorkDir}'
$global:PSScriptRoot = '{escapedWorkDir}'
$PSCommandPath = '{escapedScriptPath}'

. '{escapedScriptPath}'{argsStr}
";
                    await EncodingHelper.WriteTextUtf8BomAsync(tempRunnerFile, runnerCode);

                    psi.FileName = GetPowerShellPath(false, script.CustomInterpreterPath);
                    psi.Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempRunnerFile}\"";
                }
                else // Batch / CMD / Other
                {
                    psi.FileName = "cmd.exe";
                    psi.Arguments = $"/c \"cd /d \"{workDir}\" && chcp 65001 >nul && \"{script.FilePath}\"{argsStr}\"";
                }

                using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
                lock (_lock)
                {
                    _currentProcess = proc;
                }

                proc.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) AppendOutput(e.Data, false);
                };

                proc.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) AppendOutput(e.Data, true);
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                using (cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!proc.HasExited)
                        {
                            proc.Kill(entireProcessTree: true);
                        }
                    }
                    catch { }
                }))
                {
                    await proc.WaitForExitAsync(cancellationToken);
                }

                stopwatch.Stop();
                result.EndTime = DateTime.UtcNow;
                result.Duration = stopwatch.Elapsed;
                result.ExitCode = proc.ExitCode;
                result.Status = proc.ExitCode == 0 ? ExecutionStatus.Success : ExecutionStatus.Failed;

                AppendOutput(new string('-', 50), false);
                if (proc.ExitCode != 0)
                {
                    AppendOutput($"> Процесс завершился с ошибкой (код {proc.ExitCode}). Длительность: {stopwatch.Elapsed.TotalSeconds:F2} сек.", true);
                }
                else
                {
                    AppendOutput($"> Процесс успешно завершен (код 0). Длительность: {stopwatch.Elapsed.TotalSeconds:F2} сек.", false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            result.EndTime = DateTime.UtcNow;
            result.Duration = stopwatch.Elapsed;
            result.ExitCode = -999;
            result.Status = ExecutionStatus.Cancelled;
            AppendOutput("> Выполнение было остановлено пользователем.", true);
        }
        catch (Win32Exception w32Ex) when (w32Ex.NativeErrorCode == 1223) // ERROR_CANCELLED (UAC prompt rejected)
        {
            stopwatch.Stop();
            result.EndTime = DateTime.UtcNow;
            result.Duration = stopwatch.Elapsed;
            result.ExitCode = 1223;
            result.Status = ExecutionStatus.Cancelled;
            result.ErrorMessage = "Запуск отменен: запрос прав администратора (UAC) отклонен пользователем.";
            AppendOutput($"> {result.ErrorMessage}", true);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.EndTime = DateTime.UtcNow;
            result.Duration = stopwatch.Elapsed;
            result.ExitCode = -1;
            result.Status = ExecutionStatus.Failed;
            result.ErrorMessage = ex.Message;
            AppendOutput($"> Ошибка при выполнении: {ex.Message}", true);
            if (ex.InnerException != null)
            {
                AppendOutput($"> Детали: {ex.InnerException.Message}", true);
            }
        }
        finally
        {
            lock (_lock)
            {
                _currentProcess = null;
            }
            CurrentlyRunningScript = null;
            ExecutionStateChanged?.Invoke(this, false);

            if (!string.IsNullOrWhiteSpace(tempRunnerFile) && File.Exists(tempRunnerFile))
            {
                try { File.Delete(tempRunnerFile); } catch { }
            }

            if (!string.IsNullOrWhiteSpace(tempLogFile) && File.Exists(tempLogFile))
            {
                try { File.Delete(tempLogFile); } catch { }
            }
        }

        result.Output = fullOutput.ToString();
        return result;
    }

    public void StopCurrentProcess()
    {
        lock (_lock)
        {
            try
            {
                if (_currentProcess is { HasExited: false })
                {
                    _currentProcess.Kill(entireProcessTree: true);
                }
            }
            catch { }
        }
    }
}
