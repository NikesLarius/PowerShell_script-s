using System.ComponentModel;

namespace ScriptHub.Models;

public enum ScriptType
{
    [Description("PowerShell (.ps1)")]
    PowerShell,

    [Description("Batch (.bat)")]
    Batch,

    [Description("CMD (.cmd)")]
    Cmd,

    [Description("Другой")]
    Other
}

public enum WorkingDirectoryMode
{
    [Description("Папка со скриптом")]
    ScriptDirectory,

    [Description("Пользовательская папка")]
    CustomDirectory,

    [Description("Папка приложения")]
    AppDirectory,

    [Description("Системная по умолчанию")]
    SystemDefault
}

public enum TileSize
{
    [Description("Стандартная")]
    Standard,

    [Description("Широкая")]
    Wide,

    [Description("Компактная")]
    Compact
}

public enum ExecutionStatus
{
    [Description("Не запускался")]
    Idle,

    [Description("Выполняется")]
    Running,

    [Description("Успешно")]
    Success,

    [Description("Ошибка")]
    Failed,

    [Description("Остановлен")]
    Cancelled
}

public enum ThemeMode
{
    [Description("Как в системе")]
    System,

    [Description("Светлая")]
    Light,

    [Description("Темная")]
    Dark
}

public enum ScriptSortOrder
{
    [Description("По названию (А-Я)")]
    NameAsc,

    [Description("По названию (Я-А)")]
    NameDesc,

    [Description("По типу скрипта")]
    ByType,

    [Description("По категории")]
    ByCategory,

    [Description("По последнему запуску")]
    ByLastRun,

    [Description("Пользовательский порядок")]
    CustomOrder
}
