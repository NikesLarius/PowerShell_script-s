using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using ScriptHub.Helpers;
using ScriptHub.Models;
using ScriptHub.Services.Contracts;

namespace ScriptHub.Services;

public class StorageService : IStorageService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    public string RootDirectory { get; }
    public string ScriptsDirectory => Path.Combine(RootDirectory, "Scripts");
    public string PowerShellScriptsDirectory => Path.Combine(ScriptsDirectory, "PowerShell");
    public string CmdScriptsDirectory => Path.Combine(ScriptsDirectory, "CMD");
    public string DataDirectory => Path.Combine(RootDirectory, "Data");
    public string LogsDirectory => Path.Combine(RootDirectory, "Logs");
    public string BackupsDirectory => Path.Combine(RootDirectory, "Backups");

    private string ScriptsJsonPath => Path.Combine(DataDirectory, "scripts.json");
    private string CategoriesJsonPath => Path.Combine(DataDirectory, "categories.json");
    private string ConfigJsonPath => Path.Combine(DataDirectory, "appsettings.json");

    public StorageService()
    {
        RootDirectory = ResolveProjectRootDirectory();
    }

    private static string ResolveProjectRootDirectory()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        // 1. Search up the directory tree for ScriptHub.sln (Project Workspace Root)
        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ScriptHub.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        // 2. If running standalone from publish/ or any portable folder:
        // Use the folder of the executable
        return baseDir;
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ScriptsDirectory);
        Directory.CreateDirectory(PowerShellScriptsDirectory);
        Directory.CreateDirectory(CmdScriptsDirectory);
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(BackupsDirectory);

        // Auto-migrate from %LocalAppData%\ScriptHub if local project data is empty
        var oldLocalAppDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScriptHub");
        if (Directory.Exists(oldLocalAppDir) && !File.Exists(ScriptsJsonPath))
        {
            try
            {
                var oldScriptsJson = Path.Combine(oldLocalAppDir, "Data", "scripts.json");
                if (File.Exists(oldScriptsJson))
                {
                    var json = await File.ReadAllTextAsync(oldScriptsJson);
                    var oldScripts = JsonSerializer.Deserialize<List<ScriptModel>>(json, _jsonOptions);
                    if (oldScripts != null && oldScripts.Count > 0)
                    {
                        foreach (var s in oldScripts)
                        {
                            if (!string.IsNullOrWhiteSpace(s.FilePath) && File.Exists(s.FilePath))
                            {
                                var sub = s.ScriptType == ScriptType.PowerShell ? PowerShellScriptsDirectory : CmdScriptsDirectory;
                                var newFile = Path.Combine(sub, Path.GetFileName(s.FilePath));
                                if (!File.Exists(newFile))
                                {
                                    File.Copy(s.FilePath, newFile, true);
                                }
                                s.FilePath = newFile;
                            }
                        }
                        await SaveScriptsAsync(oldScripts);
                    }
                }

                var oldCatJson = Path.Combine(oldLocalAppDir, "Data", "categories.json");
                if (File.Exists(oldCatJson) && !File.Exists(CategoriesJsonPath))
                {
                    File.Copy(oldCatJson, CategoriesJsonPath, true);
                }
            }
            catch { }
        }

        // Seed default categories if not existing
        if (!File.Exists(CategoriesJsonPath))
        {
            var defaultCategories = GetDefaultCategories();
            await SaveCategoriesAsync(defaultCategories);
        }

        // Seed demo script if no scripts file exists
        if (!File.Exists(ScriptsJsonPath))
        {
            var categories = await LoadCategoriesAsync();
            var fileCategory = categories.FirstOrDefault(c => c.Name == "Файлы") ?? categories.FirstOrDefault();
            
            var demoScriptPath = Path.Combine(PowerShellScriptsDirectory, "Sort-FilesByType.ps1");
            const string demoCode = @"# Скрипт сортировки файлов по расширениям

Get-ChildItem -File | ForEach-Object {
    $ext = if ($_.Extension) { $_.Extension.TrimStart('.').ToLower() } else { 'Без_расширения' }
    New-Item -ItemType Directory -Name $ext -Force | Out-Null
    Move-Item -Path $_.FullName -Destination $ext
}
";
            await EncodingHelper.WriteTextUtf8BomAsync(demoScriptPath, demoCode);

            var demoScript = new ScriptModel
            {
                Id = Guid.NewGuid().ToString(),
                Title = "Сортировка файлов по типам",
                Description = "Создает папки по расширениям файлов и перемещает файлы в соответствующие папки.",
                FilePath = demoScriptPath,
                ScriptType = ScriptType.PowerShell,
                CategoryId = fileCategory?.Id ?? string.Empty,
                Icon = "FolderZip24",
                AccentColor = "#0078D4",
                TileSize = TileSize.Standard,
                IsFavorite = true,
                RunAsAdmin = false,
                WorkingDirectoryMode = WorkingDirectoryMode.CustomDirectory,
                CustomWorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads",
                Arguments = "",
                OrderIndex = 0,
                CreatedAt = DateTime.UtcNow
            };

            await SaveScriptsAsync(new List<ScriptModel> { demoScript });
        }

        if (!File.Exists(ConfigJsonPath))
        {
            await SaveConfigAsync(new AppConfigModel());
        }
    }

    private List<CategoryModel> GetDefaultCategories()
    {
        return new List<CategoryModel>
        {
            new() { Id = "cat-files", Name = "Файлы", Icon = "DocumentFolder24", ColorHex = "#0078D4", IsSystem = true },
            new() { Id = "cat-network", Name = "Сеть", Icon = "Globe24", ColorHex = "#008272", IsSystem = true },
            new() { Id = "cat-windows", Name = "Windows", Icon = "Desktop24", ColorHex = "#0063B1", IsSystem = true },
            new() { Id = "cat-1c", Name = "1С", Icon = "AppGeneric24", ColorHex = "#D83B01", IsSystem = true },
            new() { Id = "cat-admin", Name = "Администрирование", Icon = "Wrench24", ColorHex = "#498205", IsSystem = true },
            new() { Id = "cat-cleanup", Name = "Очистка", Icon = "Broom24", ColorHex = "#E81123", IsSystem = true },
            new() { Id = "cat-backup", Name = "Резервное копирование", Icon = "ArrowDownload24", ColorHex = "#881798", IsSystem = true },
            new() { Id = "cat-other", Name = "Другое", Icon = "Folder24", ColorHex = "#5C2D91", IsSystem = true }
        };
    }

    public async Task<List<ScriptModel>> LoadScriptsAsync()
    {
        if (!File.Exists(ScriptsJsonPath)) return new List<ScriptModel>();
        try
        {
            var json = await EncodingHelper.ReadTextAutoEncodingAsync(ScriptsJsonPath);
            var scripts = JsonSerializer.Deserialize<List<ScriptModel>>(json, _jsonOptions) ?? new List<ScriptModel>();

            // Auto-resolve relative paths when project folder is moved or copied
            foreach (var s in scripts)
            {
                if (!string.IsNullOrWhiteSpace(s.FilePath))
                {
                    if (!File.Exists(s.FilePath))
                    {
                        var fileName = Path.GetFileName(s.FilePath);
                        var sub = s.ScriptType == ScriptType.PowerShell ? "PowerShell" : "CMD";
                        var candidate1 = Path.Combine(ScriptsDirectory, sub, fileName);
                        var candidate2 = Path.Combine(ScriptsDirectory, fileName);
                        var candidate3 = Path.Combine(RootDirectory, fileName);

                        if (File.Exists(candidate1)) s.FilePath = candidate1;
                        else if (File.Exists(candidate2)) s.FilePath = candidate2;
                        else if (File.Exists(candidate3)) s.FilePath = candidate3;
                    }
                }
            }

            return scripts;
        }
        catch
        {
            return new List<ScriptModel>();
        }
    }

    public async Task SaveScriptsAsync(IEnumerable<ScriptModel> scripts)
    {
        var json = JsonSerializer.Serialize(scripts, _jsonOptions);
        await EncodingHelper.WriteTextUtf8BomAsync(ScriptsJsonPath, json);
    }

    public async Task<List<CategoryModel>> LoadCategoriesAsync()
    {
        if (!File.Exists(CategoriesJsonPath)) return GetDefaultCategories();
        try
        {
            var json = await EncodingHelper.ReadTextAutoEncodingAsync(CategoriesJsonPath);
            return JsonSerializer.Deserialize<List<CategoryModel>>(json, _jsonOptions) ?? GetDefaultCategories();
        }
        catch
        {
            return GetDefaultCategories();
        }
    }

    public async Task SaveCategoriesAsync(IEnumerable<CategoryModel> categories)
    {
        var json = JsonSerializer.Serialize(categories, _jsonOptions);
        await EncodingHelper.WriteTextUtf8BomAsync(CategoriesJsonPath, json);
    }

    public async Task<AppConfigModel> LoadConfigAsync()
    {
        if (!File.Exists(ConfigJsonPath)) return new AppConfigModel();
        try
        {
            var json = await EncodingHelper.ReadTextAutoEncodingAsync(ConfigJsonPath);
            return JsonSerializer.Deserialize<AppConfigModel>(json, _jsonOptions) ?? new AppConfigModel();
        }
        catch
        {
            return new AppConfigModel();
        }
    }

    public async Task SaveConfigAsync(AppConfigModel config)
    {
        var json = JsonSerializer.Serialize(config, _jsonOptions);
        await EncodingHelper.WriteTextUtf8BomAsync(ConfigJsonPath, json);
    }

    public async Task<string> ReadFileTextAsync(string path)
    {
        return await EncodingHelper.ReadTextAutoEncodingAsync(path);
    }

    public async Task WriteFileTextAsync(string path, string content)
    {
        await EncodingHelper.WriteTextUtf8BomAsync(path, content);
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    public bool FileExists(string path) => !string.IsNullOrWhiteSpace(path) && File.Exists(path);
}
