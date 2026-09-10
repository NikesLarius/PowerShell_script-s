namespace ScriptHub.Models;

public class AppConfigModel
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public TileSize DefaultTileSize { get; set; } = TileSize.Standard;
    public ScriptSortOrder DefaultSortOrder { get; set; } = ScriptSortOrder.CustomOrder;
    public string PowerShellExecutable { get; set; } = "powershell.exe"; // or pwsh.exe
    public bool PreferPowerShell7 { get; set; } = false;
    public bool ConfirmBeforeExecution { get; set; } = false;
    public bool ConfirmBeforeDeletion { get; set; } = true;
    public bool AutoScrollConsole { get; set; } = true;
    public bool ClearConsoleOnStart { get; set; } = true;
    public string DefaultWorkingDirectory { get; set; } = string.Empty;
    public int MaxHistoryEntries { get; set; } = 100;
    public double WindowOpacity { get; set; } = 1.0;
}
