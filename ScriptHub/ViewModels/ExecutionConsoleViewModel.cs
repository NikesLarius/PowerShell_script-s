using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ScriptHub.Helpers;
using ScriptHub.Models;
using ScriptHub.Services.Contracts;

namespace ScriptHub.ViewModels;

public class ExecutionConsoleViewModel : ViewModelBase
{
    private readonly IProcessService _processService;
    private readonly StringBuilder _logBuffer = new();
    private readonly DispatcherTimer _timer;
    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;

    private string _scriptTitle = "Интерактивный терминал";
    private string _scriptType = "PowerShell";
    private string _workingDirectory = "";
    private ExecutionStatus _status = ExecutionStatus.Idle;
    private string _logText = "";
    private string _inputText = "";
    private ScriptType _terminalType = ScriptType.PowerShell;
    private DateTime _startTime;
    private TimeSpan _duration = TimeSpan.Zero;
    private int? _exitCode;
    private bool _isOpen = false;
    private bool _autoScroll = true;

    public string ScriptTitle
    {
        get => _scriptTitle;
        set => SetProperty(ref _scriptTitle, value);
    }

    public string ScriptTypeHeader
    {
        get => _scriptType;
        set => SetProperty(ref _scriptType, value);
    }

    public string WorkingDirectory
    {
        get => string.IsNullOrWhiteSpace(_workingDirectory) 
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) 
            : _workingDirectory;
        set
        {
            if (SetProperty(ref _workingDirectory, value))
            {
                OnPropertyChanged(nameof(CurrentPrompt));
            }
        }
    }

    public ScriptType TerminalType
    {
        get => _terminalType;
        set
        {
            if (SetProperty(ref _terminalType, value))
            {
                OnPropertyChanged(nameof(IsPowerShellActive));
                OnPropertyChanged(nameof(IsCmdActive));
                OnPropertyChanged(nameof(CurrentPrompt));
                OnPropertyChanged(nameof(PromptBadgeText));
            }
        }
    }

    public bool IsPowerShellActive
    {
        get => TerminalType == ScriptType.PowerShell;
        set { if (value) TerminalType = ScriptType.PowerShell; }
    }

    public bool IsCmdActive
    {
        get => TerminalType == ScriptType.Batch || TerminalType == ScriptType.Cmd;
        set { if (value) TerminalType = ScriptType.Batch; }
    }

    public string PromptBadgeText => TerminalType == ScriptType.PowerShell ? "PS >" : "CMD >";

    public string CurrentPrompt
    {
        get
        {
            var folder = Path.GetFileName(WorkingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(folder)) folder = WorkingDirectory;
            return TerminalType == ScriptType.PowerShell ? $"PS [{folder}]> " : $"CMD [{folder}]> ";
        }
    }

    public string InputText
    {
        get => _inputText;
        set => SetProperty(ref _inputText, value);
    }

    public ExecutionStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsRunning));
            }
        }
    }

    public bool IsRunning => _status == ExecutionStatus.Running;

    public string StatusText => _status switch
    {
        ExecutionStatus.Running => "Выполняется...",
        ExecutionStatus.Success => "Готов",
        ExecutionStatus.Failed => _exitCode.HasValue ? $"Ошибка ({_exitCode})" : "Ошибка",
        ExecutionStatus.Cancelled => "Остановлено",
        _ => "Готов"
    };

    public string LogText
    {
        get => _logText;
        set => SetProperty(ref _logText, value);
    }

    public TimeSpan Duration
    {
        get => _duration;
        set
        {
            if (SetProperty(ref _duration, value))
            {
                OnPropertyChanged(nameof(DurationFormatted));
            }
        }
    }

    public string DurationFormatted => $"{_duration.TotalSeconds:F1} сек.";

    public int? ExitCode
    {
        get => _exitCode;
        set
        {
            if (SetProperty(ref _exitCode, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    public bool IsOpen
    {
        get => _isOpen;
        set => SetProperty(ref _isOpen, value);
    }

    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }

    public ICommand SendCommand { get; }
    public ICommand HistoryPrevCommand { get; }
    public ICommand HistoryNextCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand SetTerminalTypeCommand { get; }

    public event Action? RequestScrollToEnd;
    public event Action? RequestFocusInput;

    public ExecutionConsoleViewModel(IProcessService processService)
    {
        _processService = processService;
        _workingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _timer.Tick += (s, e) =>
        {
            if (IsRunning)
            {
                Duration = DateTime.UtcNow - _startTime;
            }
        };

        SendCommand = new AsyncRelayCommand(ExecuteInputCommandAsync);
        HistoryPrevCommand = new RelayCommand(HistoryPrev);
        HistoryNextCommand = new RelayCommand(HistoryNext);

        StopCommand = new RelayCommand(
            () => _processService.StopCurrentProcess(),
            () => IsRunning);

        ClearCommand = new RelayCommand(() =>
        {
            _logBuffer.Clear();
            LogText = "";
        });

        CopyCommand = new RelayCommand(() =>
        {
            try
            {
                if (!string.IsNullOrEmpty(LogText))
                {
                    Clipboard.SetText(LogText);
                }
            }
            catch { }
        });

        CloseCommand = new RelayCommand(() => IsOpen = false);
        SetTerminalTypeCommand = new RelayCommand<ScriptType>(type => TerminalType = type);
    }

    public void OpenTerminalSession(ScriptType? type = null, string? workDir = null)
    {
        if (type.HasValue)
        {
            TerminalType = type.Value;
        }

        if (!string.IsNullOrWhiteSpace(workDir))
        {
            WorkingDirectory = workDir;
        }

        ScriptTitle = "Интерактивный терминал";
        ScriptTypeHeader = TerminalType.ToString();
        IsOpen = true;

        if (_logBuffer.Length == 0)
        {
            AppendLog($"> Интерактивный терминал Script Hub [{TerminalType}]", false);
            AppendLog($"> Рабочая папка: {WorkingDirectory}", false);
            AppendLog($"> Введите команду и нажмите Enter для выполнения", false);
            AppendLog(new string('-', 50), false);
        }

        RequestFocusInput?.Invoke();
    }

    public void PrepareForExecution(ScriptModel script)
    {
        _logBuffer.Clear();
        LogText = "";
        ScriptTitle = script.Title;
        ScriptTypeHeader = script.ScriptType.ToString();
        TerminalType = script.ScriptType == ScriptType.PowerShell ? ScriptType.PowerShell : ScriptType.Batch;
        WorkingDirectory = _processService.ResolveWorkingDirectory(script);
        ExitCode = null;
        Status = ExecutionStatus.Running;
        _startTime = DateTime.UtcNow;
        Duration = TimeSpan.Zero;
        IsOpen = true;

        _timer.Start();
    }

    public void AppendLog(string text, bool isError)
    {
        _logBuffer.AppendLine(text);
        LogText = _logBuffer.ToString();
        RequestScrollToEnd?.Invoke();
    }

    public void FinishExecution(ExecutionResult result)
    {
        _timer.Stop();
        ExitCode = result.ExitCode;
        Duration = result.Duration;
        Status = result.Status;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(DurationFormatted));
        RequestFocusInput?.Invoke();
    }

    private async Task ExecuteInputCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText)) return;

        var cmd = InputText.Trim();
        InputText = "";

        // Add to history
        if (_commandHistory.Count == 0 || _commandHistory[^1] != cmd)
        {
            _commandHistory.Add(cmd);
        }
        _historyIndex = _commandHistory.Count;

        // If a process is actively running and waiting for input:
        if (IsRunning)
        {
            AppendLog(cmd, false);
            _processService.SendInput(cmd);
            return;
        }

        // Special built-in commands: clear / cls
        if (cmd.Equals("clear", StringComparison.OrdinalIgnoreCase) || cmd.Equals("cls", StringComparison.OrdinalIgnoreCase))
        {
            _logBuffer.Clear();
            LogText = "";
            return;
        }

        // Execute as interactive command
        ScriptTitle = cmd;
        ScriptTypeHeader = TerminalType.ToString();
        ExitCode = null;
        Status = ExecutionStatus.Running;
        _startTime = DateTime.UtcNow;
        Duration = TimeSpan.Zero;
        _timer.Start();

        try
        {
            var result = await _processService.ExecuteCommandAsync(
                cmd,
                TerminalType,
                WorkingDirectory,
                runAsAdmin: false,
                (line, isErr) => AppendLog(line, isErr));

            FinishExecution(result);
        }
        catch (Exception ex)
        {
            _timer.Stop();
            Status = ExecutionStatus.Failed;
            AppendLog($"> Ошибка: {ex.Message}", true);
        }
    }

    private void HistoryPrev()
    {
        if (_commandHistory.Count == 0) return;

        if (_historyIndex > 0)
        {
            _historyIndex--;
        }
        else if (_historyIndex == -1 || _historyIndex >= _commandHistory.Count)
        {
            _historyIndex = _commandHistory.Count - 1;
        }

        if (_historyIndex >= 0 && _historyIndex < _commandHistory.Count)
        {
            InputText = _commandHistory[_historyIndex];
        }
    }

    private void HistoryNext()
    {
        if (_commandHistory.Count == 0) return;

        if (_historyIndex >= 0 && _historyIndex < _commandHistory.Count - 1)
        {
            _historyIndex++;
            InputText = _commandHistory[_historyIndex];
        }
        else
        {
            _historyIndex = _commandHistory.Count;
            InputText = "";
        }
    }
}
