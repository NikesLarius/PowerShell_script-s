using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ScriptHub.Models;

namespace ScriptHub.Services.Contracts;

public interface IScriptService
{
    IReadOnlyList<ScriptModel> GetAllScripts();
    IReadOnlyList<CategoryModel> GetAllCategories();
    AppConfigModel GetConfig();
    
    Task InitializeAsync();
    Task SaveScriptAsync(ScriptModel script, string? codeContent = null);
    Task DeleteScriptAsync(string scriptId, bool deleteFileFromDisk);
    Task<string> LoadScriptContentAsync(string filePath);
    Task SaveScriptContentAsync(string filePath, string content);
    Task UpdateScriptOrderAsync(IEnumerable<ScriptModel> scripts);
    Task UpdateConfigAsync(AppConfigModel config);
    
    Task AddCategoryAsync(CategoryModel category);
    Task DeleteCategoryAsync(string categoryId);
    
    bool CheckFileExists(string filePath);
    bool IsInsideScriptsFolder(string filePath);
    string CreateNewScriptFile(string title, ScriptType scriptType, string content);
    Task<string> EnsureScriptInScriptsFolderAsync(string? existingPath, string title, ScriptType scriptType, string content);
}
