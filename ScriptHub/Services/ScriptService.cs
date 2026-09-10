using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ScriptHub.Helpers;
using ScriptHub.Models;
using ScriptHub.Services.Contracts;

namespace ScriptHub.Services;

public class ScriptService : IScriptService
{
    private readonly IStorageService _storageService;
    private readonly List<ScriptModel> _scripts = new();
    private readonly List<CategoryModel> _categories = new();
    private AppConfigModel _config = new();

    public ScriptService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public IReadOnlyList<ScriptModel> GetAllScripts() => _scripts.AsReadOnly();
    public IReadOnlyList<CategoryModel> GetAllCategories() => _categories.AsReadOnly();
    public AppConfigModel GetConfig() => _config;

    public async Task InitializeAsync()
    {
        await _storageService.InitializeAsync();
        
        _scripts.Clear();
        _scripts.AddRange(await _storageService.LoadScriptsAsync());

        // Ensure all scripts are placed in Scripts folder
        bool modified = false;
        foreach (var s in _scripts)
        {
            if (!string.IsNullOrWhiteSpace(s.FilePath) && !_storageService.IsPathInsideScriptsDirectory(s.FilePath) && File.Exists(s.FilePath))
            {
                try
                {
                    var content = await _storageService.ReadFileTextAsync(s.FilePath);
                    s.FilePath = _storageService.EnsureScriptInScriptsDirectory(s.FilePath, s.Title, s.ScriptType, content);
                    modified = true;
                }
                catch { }
            }
        }
        if (modified)
        {
            await _storageService.SaveScriptsAsync(_scripts);
        }

        _categories.Clear();
        _categories.AddRange(await _storageService.LoadCategoriesAsync());

        _config = await _storageService.LoadConfigAsync();
    }

    public async Task SaveScriptAsync(ScriptModel script, string? codeContent = null)
    {
        if (codeContent != null)
        {
            if (string.IsNullOrWhiteSpace(script.FilePath) || !_storageService.IsPathInsideScriptsDirectory(script.FilePath))
            {
                script.FilePath = _storageService.EnsureScriptInScriptsDirectory(script.FilePath, script.Title, script.ScriptType, codeContent);
            }
            else
            {
                await _storageService.WriteFileTextAsync(script.FilePath, codeContent);
            }
        }
        else if (!string.IsNullOrWhiteSpace(script.FilePath) && !_storageService.IsPathInsideScriptsDirectory(script.FilePath) && File.Exists(script.FilePath))
        {
            var content = await _storageService.ReadFileTextAsync(script.FilePath);
            script.FilePath = _storageService.EnsureScriptInScriptsDirectory(script.FilePath, script.Title, script.ScriptType, content);
        }

        var existingIndex = _scripts.FindIndex(s => s.Id == script.Id);
        if (existingIndex >= 0)
        {
            _scripts[existingIndex] = script;
        }
        else
        {
            script.OrderIndex = _scripts.Count > 0 ? _scripts.Max(s => s.OrderIndex) + 1 : 0;
            _scripts.Add(script);
        }

        await _storageService.SaveScriptsAsync(_scripts);
    }

    public async Task DeleteScriptAsync(string scriptId, bool deleteFileFromDisk)
    {
        var script = _scripts.FirstOrDefault(s => s.Id == scriptId);
        if (script == null) return;

        if (deleteFileFromDisk && !string.IsNullOrWhiteSpace(script.FilePath))
        {
            _storageService.DeleteFile(script.FilePath);
        }

        _scripts.Remove(script);
        await _storageService.SaveScriptsAsync(_scripts);
    }

    public async Task<string> LoadScriptContentAsync(string filePath)
    {
        return await _storageService.ReadFileTextAsync(filePath);
    }

    public async Task SaveScriptContentAsync(string filePath, string content)
    {
        await _storageService.WriteFileTextAsync(filePath, content);
    }

    public async Task UpdateScriptOrderAsync(IEnumerable<ScriptModel> scripts)
    {
        _scripts.Clear();
        int index = 0;
        foreach (var s in scripts)
        {
            s.OrderIndex = index++;
            _scripts.Add(s);
        }
        await _storageService.SaveScriptsAsync(_scripts);
    }

    public async Task UpdateConfigAsync(AppConfigModel config)
    {
        _config = config;
        await _storageService.SaveConfigAsync(_config);
    }

    public async Task AddCategoryAsync(CategoryModel category)
    {
        _categories.Add(category);
        await _storageService.SaveCategoriesAsync(_categories);
    }

    public async Task DeleteCategoryAsync(string categoryId)
    {
        var cat = _categories.FirstOrDefault(c => c.Id == categoryId);
        if (cat != null)
        {
            _categories.Remove(cat);
            await _storageService.SaveCategoriesAsync(_categories);
        }
    }

    public bool CheckFileExists(string filePath) => _storageService.FileExists(filePath);

    public bool IsInsideScriptsFolder(string filePath) => _storageService.IsPathInsideScriptsDirectory(filePath);

    public Task<string> EnsureScriptInScriptsFolderAsync(string? existingPath, string title, ScriptType scriptType, string content)
    {
        var resultPath = _storageService.EnsureScriptInScriptsDirectory(existingPath, title, scriptType, content);
        return Task.FromResult(resultPath);
    }

    public string CreateNewScriptFile(string title, ScriptType scriptType, string content)
    {
        var safeName = string.Join("_", title.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Script_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

        var ext = scriptType switch
        {
            ScriptType.PowerShell => ".ps1",
            ScriptType.Batch => ".bat",
            ScriptType.Cmd => ".cmd",
            _ => ".ps1"
        };

        var targetFolder = scriptType == ScriptType.PowerShell
            ? _storageService.PowerShellScriptsDirectory
            : _storageService.CmdScriptsDirectory;

        var fullPath = Path.Combine(targetFolder, safeName + ext);
        int counter = 1;
        while (File.Exists(fullPath))
        {
            fullPath = Path.Combine(targetFolder, $"{safeName}_{counter++}{ext}");
        }

        EncodingHelper.WriteTextUtf8Bom(fullPath, content);
        return fullPath;
    }
}
