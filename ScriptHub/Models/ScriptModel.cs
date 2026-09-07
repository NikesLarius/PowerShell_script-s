using System;
using System.Text.Json.Serialization;

namespace ScriptHub.Models;

public class ScriptModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public ScriptType ScriptType { get; set; } = ScriptType.PowerShell;
    public string CategoryId { get; set; } = string.Empty;
    public string Icon { get; set; } = "DocumentCode24";
    public string AccentColor { get; set; } = "#0078D4";
    public TileSize TileSize { get; set; } = TileSize.Standard;
    public bool IsFavorite { get; set; } = false;
    public bool RunAsAdmin { get; set; } = false;
    public WorkingDirectoryMode WorkingDirectoryMode { get; set; } = WorkingDirectoryMode.ScriptDirectory;
    public string CustomWorkingDirectory { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string CustomInterpreterPath { get; set; } = string.Empty;
    public int OrderIndex { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastRunAt { get; set; }
    public int? LastExitCode { get; set; }
    public TimeSpan? LastDuration { get; set; }
    public ExecutionStatus LastStatus { get; set; } = ExecutionStatus.Idle;

    public ScriptModel Clone()
    {
        return new ScriptModel
        {
            Id = this.Id,
            Title = this.Title,
            Description = this.Description,
            FilePath = this.FilePath,
            ScriptType = this.ScriptType,
            CategoryId = this.CategoryId,
            Icon = this.Icon,
            AccentColor = this.AccentColor,
            TileSize = this.TileSize,
            IsFavorite = this.IsFavorite,
            RunAsAdmin = this.RunAsAdmin,
            WorkingDirectoryMode = this.WorkingDirectoryMode,
            CustomWorkingDirectory = this.CustomWorkingDirectory,
            Arguments = this.Arguments,
            CustomInterpreterPath = this.CustomInterpreterPath,
            OrderIndex = this.OrderIndex,
            CreatedAt = this.CreatedAt,
            LastRunAt = this.LastRunAt,
            LastExitCode = this.LastExitCode,
            LastDuration = this.LastDuration,
            LastStatus = this.LastStatus
        };
    }
}
