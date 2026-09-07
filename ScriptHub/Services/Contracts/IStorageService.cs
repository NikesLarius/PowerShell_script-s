using System.Collections.Generic;
using System.Threading.Tasks;
using ScriptHub.Models;

namespace ScriptHub.Services.Contracts;

public interface IStorageService
{
    string RootDirectory { get; }
    string ScriptsDirectory { get; }
    string PowerShellScriptsDirectory { get; }
    string CmdScriptsDirectory { get; }
    string DataDirectory { get; }
    string LogsDirectory { get; }
    string BackupsDirectory { get; }

    Task InitializeAsync();
    
    Task<List<ScriptModel>> LoadScriptsAsync();
    Task SaveScriptsAsync(IEnumerable<ScriptModel> scripts);
    
    Task<List<CategoryModel>> LoadCategoriesAsync();
    Task SaveCategoriesAsync(IEnumerable<CategoryModel> categories);
    
    Task<AppConfigModel> LoadConfigAsync();
    Task SaveConfigAsync(AppConfigModel config);
    
    Task<string> ReadFileTextAsync(string path);
    Task WriteFileTextAsync(string path, string content);
    void DeleteFile(string path);
    bool FileExists(string path);
}
