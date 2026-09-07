using System.Collections.Generic;
using System.Threading.Tasks;
using ScriptHub.Models;

namespace ScriptHub.Services.Contracts;

public interface IHistoryService
{
    Task<List<HistoryEntryModel>> GetHistoryAsync();
    Task AddHistoryEntryAsync(HistoryEntryModel entry);
    Task ClearHistoryAsync();
    Task DeleteHistoryEntryAsync(string entryId);
}
