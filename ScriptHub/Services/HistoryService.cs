using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using ScriptHub.Models;
using ScriptHub.Services.Contracts;

namespace ScriptHub.Services;

public class HistoryService : IHistoryService
{
    private readonly IStorageService _storageService;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = true
    };

    private string HistoryFilePath => Path.Combine(_storageService.LogsDirectory, "history.json");

    public HistoryService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public async Task<List<HistoryEntryModel>> GetHistoryAsync()
    {
        if (!File.Exists(HistoryFilePath)) return new List<HistoryEntryModel>();
        try
        {
            var json = await File.ReadAllTextAsync(HistoryFilePath);
            var list = JsonSerializer.Deserialize<List<HistoryEntryModel>>(json, _jsonOptions) ?? new List<HistoryEntryModel>();
            return list.OrderByDescending(h => h.StartTime).ToList();
        }
        catch
        {
            return new List<HistoryEntryModel>();
        }
    }

    public async Task AddHistoryEntryAsync(HistoryEntryModel entry)
    {
        var history = await GetHistoryAsync();
        history.Insert(0, entry);

        // Keep last 200 entries
        if (history.Count > 200)
        {
            history = history.Take(200).ToList();
        }

        var json = JsonSerializer.Serialize(history, _jsonOptions);
        await File.WriteAllTextAsync(HistoryFilePath, json);
    }

    public async Task ClearHistoryAsync()
    {
        if (File.Exists(HistoryFilePath))
        {
            File.Delete(HistoryFilePath);
        }
        await Task.CompletedTask;
    }

    public async Task DeleteHistoryEntryAsync(string entryId)
    {
        var history = await GetHistoryAsync();
        history.RemoveAll(h => h.Id == entryId);
        var json = JsonSerializer.Serialize(history, _jsonOptions);
        await File.WriteAllTextAsync(HistoryFilePath, json);
    }
}
