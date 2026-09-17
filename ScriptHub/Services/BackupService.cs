using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using ScriptHub.Helpers;
using ScriptHub.Models;
using ScriptHub.Services.Contracts;

namespace ScriptHub.Services;

public class BackupService : IBackupService
{
    private readonly IStorageService _storageService;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    public BackupService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<string> ExportBackupAsync(string targetZipPath)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ScriptHub_Backup_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);

            // 1. Copy Data directory (settings, categories, scripts metadata)
            var targetDataDir = Path.Combine(tempDir, "Data");
            if (Directory.Exists(_storageService.DataDirectory))
            {
                CopyDirectory(_storageService.DataDirectory, targetDataDir);
            }

            // 2. Copy Scripts directory (PowerShell and CMD)
            var targetScriptsDir = Path.Combine(tempDir, "Scripts");
            var targetPsDir = Path.Combine(targetScriptsDir, "PowerShell");
            var targetCmdDir = Path.Combine(targetScriptsDir, "CMD");
            Directory.CreateDirectory(targetPsDir);
            Directory.CreateDirectory(targetCmdDir);

            if (Directory.Exists(_storageService.ScriptsDirectory))
            {
                CopyDirectory(_storageService.ScriptsDirectory, targetScriptsDir);
            }

            // Ensure any external script referenced in scripts.json is also packed
            var scripts = await _storageService.LoadScriptsAsync();
            foreach (var script in scripts)
            {
                if (!string.IsNullOrWhiteSpace(script.FilePath) && File.Exists(script.FilePath))
                {
                    var fileName = Path.GetFileName(script.FilePath);
                    var subFolder = script.ScriptType == ScriptType.PowerShell ? targetPsDir : targetCmdDir;
                    var destFile = Path.Combine(subFolder, fileName);
                    if (!File.Exists(destFile))
                    {
                        try { File.Copy(script.FilePath, destFile, true); } catch { }
                    }
                }
            }

            if (File.Exists(targetZipPath))
            {
                File.Delete(targetZipPath);
            }

            var targetZipDir = Path.GetDirectoryName(targetZipPath);
            if (!string.IsNullOrWhiteSpace(targetZipDir)) Directory.CreateDirectory(targetZipDir);

            ZipFile.CreateFromDirectory(tempDir, targetZipPath, CompressionLevel.Optimal, false);
            return await Task.FromResult(targetZipPath);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    public async Task<bool> ImportBackupAsync(string sourceZipPath)
    {
        if (!File.Exists(sourceZipPath)) return false;

        var tempDir = Path.Combine(Path.GetTempPath(), "ScriptHub_Restore_" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(sourceZipPath, tempDir, true);

            // Ensure destination directories exist
            Directory.CreateDirectory(_storageService.DataDirectory);
            Directory.CreateDirectory(_storageService.ScriptsDirectory);
            Directory.CreateDirectory(_storageService.PowerShellScriptsDirectory);
            Directory.CreateDirectory(_storageService.CmdScriptsDirectory);

            // 1. Restore Categories and Config if present in archive
            var sourceCategoriesJson = FindFileRecursive(tempDir, "categories.json");
            if (!string.IsNullOrWhiteSpace(sourceCategoriesJson) && File.Exists(sourceCategoriesJson))
            {
                var targetCatPath = Path.Combine(_storageService.DataDirectory, "categories.json");
                File.Copy(sourceCategoriesJson, targetCatPath, true);
            }

            var sourceConfigJson = FindFileRecursive(tempDir, "appsettings.json");
            if (!string.IsNullOrWhiteSpace(sourceConfigJson) && File.Exists(sourceConfigJson))
            {
                var targetConfigPath = Path.Combine(_storageService.DataDirectory, "appsettings.json");
                File.Copy(sourceConfigJson, targetConfigPath, true);
            }

            // 2. Distribute all script files (.ps1 to PowerShell, .bat/.cmd to CMD)
            var copiedScripts = new List<(string FileName, string TargetPath, ScriptType ScriptType)>();
            var allFiles = Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories);

            foreach (var filePath in allFiles)
            {
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext is not (".ps1" or ".bat" or ".cmd")) continue;

                var fileName = Path.GetFileName(filePath);
                string targetPath;
                ScriptType scriptType;

                if (ext == ".ps1")
                {
                    targetPath = Path.Combine(_storageService.PowerShellScriptsDirectory, fileName);
                    scriptType = ScriptType.PowerShell;
                }
                else
                {
                    targetPath = Path.Combine(_storageService.CmdScriptsDirectory, fileName);
                    scriptType = ext == ".cmd" ? ScriptType.Cmd : ScriptType.Batch;
                }

                File.Copy(filePath, targetPath, true);
                copiedScripts.Add((fileName, targetPath, scriptType));
            }

            // 3. Process scripts.json and generate/update tiles with Title and Description
            var sourceScriptsJson = FindFileRecursive(tempDir, "scripts.json");
            var backupScripts = new List<ScriptModel>();

            if (!string.IsNullOrWhiteSpace(sourceScriptsJson) && File.Exists(sourceScriptsJson))
            {
                try
                {
                    var json = await EncodingHelper.ReadTextAutoEncodingAsync(sourceScriptsJson);
                    var deserialized = JsonSerializer.Deserialize<List<ScriptModel>>(json, _jsonOptions);
                    if (deserialized != null)
                    {
                        backupScripts.AddRange(deserialized);
                    }
                }
                catch { }
            }

            var availableCategories = await _storageService.LoadCategoriesAsync();
            var defaultCategoryId = availableCategories.FirstOrDefault()?.Id ?? "cat-files";

            // Update existing backup scripts metadata with current local paths
            foreach (var s in backupScripts)
            {
                var fileName = !string.IsNullOrWhiteSpace(s.FilePath)
                    ? Path.GetFileName(s.FilePath)
                    : (string.IsNullOrWhiteSpace(s.Title) ? "Script.ps1" : s.Title + (s.ScriptType == ScriptType.PowerShell ? ".ps1" : ".bat"));

                var isPs = fileName.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase) || s.ScriptType == ScriptType.PowerShell;
                var isCmd = fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || s.ScriptType == ScriptType.Cmd;

                s.ScriptType = isPs ? ScriptType.PowerShell : (isCmd ? ScriptType.Cmd : ScriptType.Batch);
                var targetDir = isPs ? _storageService.PowerShellScriptsDirectory : _storageService.CmdScriptsDirectory;
                s.FilePath = Path.Combine(targetDir, fileName);

                if (string.IsNullOrWhiteSpace(s.Title))
                {
                    s.Title = Path.GetFileNameWithoutExtension(fileName);
                }

                if (string.IsNullOrWhiteSpace(s.CategoryId) || !availableCategories.Any(c => c.Id == s.CategoryId))
                {
                    s.CategoryId = defaultCategoryId;
                }

                // If description is missing, attempt to extract it from the file content
                if (string.IsNullOrWhiteSpace(s.Description) && File.Exists(s.FilePath))
                {
                    try
                    {
                        var content = await EncodingHelper.ReadTextAutoEncodingAsync(s.FilePath);
                        var (_, desc) = ScriptMetadataHelper.ExtractMetadata(s.FilePath, content);
                        s.Description = desc;
                    }
                    catch { }
                }
            }

            // For any script files in the archive that were NOT in scripts.json, create new tiles
            foreach (var copied in copiedScripts)
            {
                var alreadyTracked = backupScripts.Any(s =>
                    string.Equals(Path.GetFileName(s.FilePath), copied.FileName, StringComparison.OrdinalIgnoreCase));

                if (!alreadyTracked)
                {
                    string title = Path.GetFileNameWithoutExtension(copied.FileName);
                    string description = "Импортированный скрипт";

                    try
                    {
                        if (File.Exists(copied.TargetPath))
                        {
                            var content = await EncodingHelper.ReadTextAutoEncodingAsync(copied.TargetPath);
                            var meta = ScriptMetadataHelper.ExtractMetadata(copied.TargetPath, content);
                            if (!string.IsNullOrWhiteSpace(meta.Title)) title = meta.Title;
                            if (!string.IsNullOrWhiteSpace(meta.Description)) description = meta.Description;
                        }
                    }
                    catch { }

                    var isPs = copied.ScriptType == ScriptType.PowerShell;
                    var newScript = new ScriptModel
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = title,
                        Description = description,
                        FilePath = copied.TargetPath,
                        ScriptType = copied.ScriptType,
                        CategoryId = defaultCategoryId,
                        Icon = isPs ? "DocumentCode24" : "Terminal24",
                        AccentColor = isPs ? "#0078D4" : "#D83B01",
                        TileSize = TileSize.Standard,
                        IsFavorite = false,
                        RunAsAdmin = false,
                        WorkingDirectoryMode = WorkingDirectoryMode.ScriptDirectory,
                        CustomWorkingDirectory = "",
                        Arguments = "",
                        CustomInterpreterPath = "",
                        OrderIndex = backupScripts.Count,
                        CreatedAt = DateTime.UtcNow
                    };
                    backupScripts.Add(newScript);
                }
            }

            // Save the finalized scripts configuration
            await _storageService.SaveScriptsAsync(backupScripts);

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    private static string? FindFileRecursive(string rootDir, string targetFileName)
    {
        try
        {
            var files = Directory.GetFiles(rootDir, targetFileName, SearchOption.AllDirectories);
            return files.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists) return;

        Directory.CreateDirectory(destinationDir);

        foreach (var file in dir.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath, true);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            var newDestinationDir = Path.Combine(destinationDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestinationDir);
        }
    }
}
