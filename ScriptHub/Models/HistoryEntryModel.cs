using System;

namespace ScriptHub.Models;

public class HistoryEntryModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ScriptId { get; set; } = string.Empty;
    public string ScriptTitle { get; set; } = string.Empty;
    public ScriptType ScriptType { get; set; } = ScriptType.PowerShell;
    public string ScriptFilePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public bool RunAsAdmin { get; set; } = false;

    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public int ExitCode { get; set; } = 0;
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Idle;

    public string LogSnippet { get; set; } = string.Empty;
    public string FullLogPath { get; set; } = string.Empty;
}
