using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using ScriptHub.Services.Contracts;

namespace ScriptHub.Services;

public class BackupService : IBackupService
{
    private readonly IStorageService _storageService;

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

            // Copy Data directory
            var targetDataDir = Path.Combine(tempDir, "Data");
            if (Directory.Exists(_storageService.DataDirectory))
            {
                CopyDirectory(_storageService.DataDirectory, targetDataDir);
            }

            // Copy Scripts directory
            var targetScriptsDir = Path.Combine(tempDir, "Scripts");
            if (Directory.Exists(_storageService.ScriptsDirectory))
            {
                CopyDirectory(_storageService.ScriptsDirectory, targetScriptsDir);
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

            var sourceDataDir = Path.Combine(tempDir, "Data");
            if (Directory.Exists(sourceDataDir))
            {
                CopyDirectory(sourceDataDir, _storageService.DataDirectory);
            }

            var sourceScriptsDir = Path.Combine(tempDir, "Scripts");
            if (Directory.Exists(sourceScriptsDir))
            {
                CopyDirectory(sourceScriptsDir, _storageService.ScriptsDirectory);
            }

            return await Task.FromResult(true);
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
