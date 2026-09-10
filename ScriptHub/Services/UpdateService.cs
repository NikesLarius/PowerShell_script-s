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

    public async Task<UpdateResult> CheckForUpdatesAsync()
    {
        var isGitRepo = Directory.Exists(Path.Combine(_storageService.RootDirectory, ".git"));

        if (isGitRepo)
        {
            var fetchResult = await RunCommandAsync("git", "fetch origin main", _storageService.RootDirectory);
            if (fetchResult.ExitCode == 0)
            {
                var diffResult = await RunCommandAsync("git", "log HEAD..origin/main --oneline", _storageService.RootDirectory);
                if (diffResult.ExitCode == 0)
                {
                    var commits = diffResult.Output.Trim();
                    if (string.IsNullOrWhiteSpace(commits))
                    {
                        return new UpdateResult
                        {
                            Success = true,
                            HasUpdates = false,
                            Message = "У вас установлена самая актуальная версия из ветки main."
                        };
                    }
                    else
                    {
                        return new UpdateResult
                        {
                            Success = true,
                            HasUpdates = true,
                            Message = "Доступны свежие обновления на GitHub!",
                            Details = $"Новые коммиты:\n{commits}"
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

                return new UpdateResult
                {
                    Success = true,
                    HasUpdates = true,
                    Message = "Связь с GitHub установлена. Доступна актуальная версия ветки main.",
                    Details = $"Последний коммит ({sha}): {commitMessage}"
                };
            }
            else
            {
                return new UpdateResult
                {
                    Success = false,
                    Message = $"GitHub вернул статус {(int)response.StatusCode}: {response.ReasonPhrase}"
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
            progress?.Report("Синхронизация через Git...");
            var pullResult = await RunCommandAsync("git", "pull origin main", _storageService.RootDirectory);

            if (pullResult.ExitCode == 0)
            {
                var output = pullResult.Output.Trim();
                bool alreadyUpToDate = output.Contains("Already up to date", StringComparison.OrdinalIgnoreCase) ||
                                       output.Contains("Уже обновлено", StringComparison.OrdinalIgnoreCase);

                // Reload scripts and categories in memory
                await _scriptService.InitializeAsync();

                return new UpdateResult
                {
                    Success = true,
                    HasUpdates = !alreadyUpToDate,
                    Message = alreadyUpToDate
                        ? "Программа и скрипты уже обновлены до последней версии с GitHub."
                        : "Обновление успешно выполнено! Файлы и скрипты синхронизированы с GitHub.",
                    Details = output
                };
            }
            else
            {
                progress?.Report("Git pull вернул предупреждение. Пробуем загрузку архива...");
            }
        }

        // Fallback: download zip archive from GitHub
        try
        {
            progress?.Report("Загрузка архива репозитория с GitHub...");
            var zipBytes = await _httpClient.GetByteArrayAsync(GitHubZipArchiveUrl);

            progress?.Report("Распаковка и обновление скриптов...");
            var tempZip = Path.Combine(Path.GetTempPath(), $"ScriptHub_Update_{Guid.NewGuid():N}.zip");
            var tempExtract = Path.Combine(Path.GetTempPath(), $"ScriptHub_Extract_{Guid.NewGuid():N}");

            try
            {
                await File.WriteAllBytesAsync(tempZip, zipBytes);
                ZipFile.ExtractToDirectory(tempZip, tempExtract);

                // The root folder in zip is usually PowerShell_script-s-main
                var extractedDirs = Directory.GetDirectories(tempExtract);
                var sourceRootDir = extractedDirs.Length > 0 ? extractedDirs[0] : tempExtract;

                // Sync Scripts folder
                var sourceScriptsDir = Path.Combine(sourceRootDir, "Scripts");
                if (Directory.Exists(sourceScriptsDir))
                {
                    CopyDirectory(sourceScriptsDir, _storageService.ScriptsDirectory, overwrite: true);
                }

                // Sync Data folder if default files needed
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

                // Reload scripts and categories
                await _scriptService.InitializeAsync();

                return new UpdateResult
                {
                    Success = true,
                    HasUpdates = true,
                    Message = "Скрипты и компоненты успешно обновлены с GitHub (ветка main)!"
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
                Message = $"Ошибка при обновлении с GitHub: {ex.Message}"
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
