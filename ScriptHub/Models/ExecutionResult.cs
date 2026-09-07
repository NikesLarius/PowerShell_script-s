using System;

namespace ScriptHub.Models;

public class ExecutionResult
{
    public bool Success => ExitCode == 0 && Status == ExecutionStatus.Success;
    public int ExitCode { get; set; }
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Idle;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Output { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}
