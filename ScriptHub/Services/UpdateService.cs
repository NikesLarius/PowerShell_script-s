using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using ScriptHub.Services.Contracts;

namespace ScriptHub.Services;

public class UpdateService : IUpdateService
{
    private const string GitHubRepoUrl = "https://github.com/NikesLarius/PowerShell_script-s";
    private const string GitHubApiCommitsUrl = "https://api.github.com/repos/NikesLarius/PowerShell_script-s/commits/main";
    private const string GitHubZipArchiveUrl = "https://github.com/NikesLarius/PowerShell_script-s/archive/refs/heads/main.zip";

    private readonly IStorageService _storageService;
    private readonly IScriptService _scriptService;
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static UpdateService()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ScriptHub-Updater/1.4");
    }

    public UpdateService(IStorageService storageService, IScriptService scriptService)
    {
        _storageService = storageService;
        _scriptService = scriptService;
    }

    public void OpenGitHubRepository()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GitHubRepoUrl,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private async Task<string> GetCurrentCommitSummaryAsync()
    {
        try
        {
            var isGitRepo = Directory.Exists(Path.Combine(_storageService.RootDirectory, ".git"));
            if (!isGitRepo) return "";

            var res = await RunCommandAsync("git", "log -1 --format=\"%h (%cd): %s\" --date=short", _storageService.RootDirectory);
            return res.ExitCode == 0 ? res.Output.Trim() : "";
        }
        catch
        {
            return "";
        }
    }

    public async Task<UpdateResult> CheckForUpdatesAsync()
    {
        var isGitRepo = Directory.Exists(Path.Combine(_storageService.RootDirectory, ".git"));

        if (isGitRepo)
        {
            var fetchResult = await RunCommandAsync("git", "fetch origin main", _storageService.RootDirectory);
            if (fetchResult.ExitCode == 0)
            {
                var diffResult = await RunCommandAsync("git", "log HEAD..origin/main --format=\"• %h: %s\" -n 10", _storageService.RootDirectory);
                if (diffResult.ExitCode == 0)
                {
                    var commits = diffResult.Output.Trim();
                    var curCommit = await GetCurrentCommitSummaryAsync();

                    if (string.IsNullOrWhiteSpace(commits))
                    {
                        return new UpdateResult
                        {
                            Success = true,
                            HasUpdates = false,
                            Message = "Установлена актуальная версия. Обновлений нет.",
                            Details = !string.IsNullOrWhiteSpace(curCommit) ? $"Текущая версия:\n• {curCommit}" : ""
                        };
                    }
                    else
                    {
                        return new UpdateResult
                        {
                            Success = true,
                            HasUpdates = true,
                            Message = "Доступно новое обновление!",
                            Details = $"Доступные изменения:\n{commits}" + (!string.IsNullOrWhiteSpace(curCommit) ? $"\n\nТекущая версия:\n• {curCommit}" : "")
                        };
                    }
                }
            }
        }

        // Fallback: Check via GitHub API
        try
        {
            using var response = await _httpClient.GetAsync(GitHubApiCommitsUrl);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var sha = root.TryGetProperty("sha", out var shaProp) ? shaProp.GetString()?[..7] : "";
                var commitMessage = root.TryGetProperty("commit", out var commitProp) &&
                                    commitProp.TryGetProperty("message", out var msgProp)
                    ? msgProp.GetString()
                    : "";

                var curCommit = await GetCurrentCommitSummaryAsync();
                return new UpdateResult
                {
                    Success = true,
                    HasUpdates = true,
                    Message = "Доступно новое обновление!",
                    Details = $"Последнее изменение ({sha}):\n• {commitMessage}" + (!string.IsNullOrWhiteSpace(curCommit) ? $"\n\nТекущая версия:\n• {curCommit}" : "")
                };
            }
            else
            {
                return new UpdateResult
                {
                    Success = false,
                    Message = $"Сервер обновлений вернул статус {(int)response.StatusCode}: {response.ReasonPhrase}"
                };
            }
        }
        catch (Exception ex)
        {
            return new UpdateResult
            {
                Success = false,
                Message = $"Не удалось проверить обновления: {ex.Message}"
            };
        }
    }

    public async Task<UpdateResult> UpdateFromGitHubAsync(IProgress<string>? progress = null)
    {
        var isGitRepo = Directory.Exists(Path.Combine(_storageService.RootDirectory, ".git"));

        if (isGitRepo)
        {
            progress?.Report("Проверка доступных обновлений...");
            await RunCommandAsync("git", "fetch origin main", _storageService.RootDirectory);
            var diffResult = await RunCommandAsync("git", "log HEAD..origin/main --format=\"• %h: %s\" -n 10", _storageService.RootDirectory);
            var incomingCommits = diffResult.ExitCode == 0 ? diffResult.Output.Trim() : "";

            progress?.Report("Синхронизация файлов программы...");
            var pullResult = await RunCommandAsync("git", "pull origin main", _storageService.RootDirectory);

            if (pullResult.ExitCode == 0)
            {
                var output = pullResult.Output.Trim();
                bool alreadyUpToDate = output.Contains("Already up to date", StringComparison.OrdinalIgnoreCase) ||
                                       output.Contains("Уже обновлено", StringComparison.OrdinalIgnoreCase);

                // Reload scripts and categories in memory
                await _scriptService.InitializeAsync();
                var curCommit = await GetCurrentCommitSummaryAsync();

                if (!alreadyUpToDate && !string.IsNullOrWhiteSpace(incomingCommits))
                {
                    return new UpdateResult
                    {
                        Success = true,
                        HasUpdates = true,
                        Message = "Программа и скрипты успешно обновлены!",
                        Details = $"Установленные изменения:\n{incomingCommits}" + (!string.IsNullOrWhiteSpace(curCommit) ? $"\n\nТекущая версия:\n• {curCommit}" : "")
                    };
                }
                else
                {
                    return new UpdateResult
                    {
                        Success = true,
                        HasUpdates = false,
                        Message = "Установлена актуальная версия. Обновлений нет.",
                        Details = !string.IsNullOrWhiteSpace(curCommit) ? $"Текущая версия:\n• {curCommit}" : ""
                    };
                }
            }
            else
            {
                progress?.Report("Предупреждение синхронизации. Пробуем загрузку архива...");
            }
        }

        // Fallback: download zip archive from GitHub
        try
        {
            progress?.Report("Загрузка архива обновлений...");
            var zipBytes = await _httpClient.GetByteArrayAsync(GitHubZipArchiveUrl);

            progress?.Report("Распаковка и обновление скриптов...");
            var tempZip = Path.Combine(Path.GetTempPath(), $"ScriptHub_Update_{Guid.NewGuid():N}.zip");
            var tempExtract = Path.Combine(Path.GetTempPath(), $"ScriptHub_Extract_{Guid.NewGuid():N}");

            try
            {
                await File.WriteAllBytesAsync(tempZip, zipBytes);
                ZipFile.ExtractToDirectory(tempZip, tempExtract);

                var extractedDirs = Directory.GetDirectories(tempExtract);
                var sourceRootDir = extractedDirs.Length > 0 ? extractedDirs[0] : tempExtract;

                var sourceScriptsDir = Path.Combine(sourceRootDir, "Scripts");
                if (Directory.Exists(sourceScriptsDir))
                {
                    CopyDirectory(sourceScriptsDir, _storageService.ScriptsDirectory, overwrite: true);
                }

                var sourceDataDir = Path.Combine(sourceRootDir, "Data");
                if (Directory.Exists(sourceDataDir))
                {
                    var catSrc = Path.Combine(sourceDataDir, "categories.json");
                    var catDst = Path.Combine(_storageService.DataDirectory, "categories.json");
                    if (File.Exists(catSrc) && !File.Exists(catDst))
                    {
                        File.Copy(catSrc, catDst, false);
                    }
                }

                await _scriptService.InitializeAsync();
                var curCommit = await GetCurrentCommitSummaryAsync();

                return new UpdateResult
                {
                    Success = true,
                    HasUpdates = true,
                    Message = "Скрипты и компоненты успешно обновлены!",
                    Details = !string.IsNullOrWhiteSpace(curCommit) ? $"Текущая версия:\n• {curCommit}" : ""
                };
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                try { if (Directory.Exists(tempExtract)) Directory.Delete(tempExtract, true); } catch { }
            }
        }
        catch (Exception ex)
        {
            return new UpdateResult
            {
                Success = false,
                Message = $"Ошибка загрузки обновлений: {ex.Message}"
            };
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir, bool overwrite)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var destFile = Path.Combine(destinationDir, Path.GetFileName(file));
            File.Copy(file, destFile, overwrite);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destinationDir, Path.GetFileName(subDir));
            CopyDirectory(subDir, destSubDir, overwrite);
        }
    }

    private static async Task<(int ExitCode, string Output)> RunCommandAsync(string command, string arguments, string workingDir)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi };
            var outputSb = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null) outputSb.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null) outputSb.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();
            return (process.ExitCode, outputSb.ToString());
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }
}
