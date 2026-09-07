using System;
using System.Text;
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

    private string _scriptTitle = "";
    private string _scriptType = "";
    private string _workingDirectory = "";
    private ExecutionStatus _status = ExecutionStatus.Idle;
    private string _logText = "";
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

    public string ScriptType
    {
        get => _scriptType;
        set => SetProperty(ref _scriptType, value);
    }

    public string WorkingDirectory
    {
        get => _workingDirectory;
        set => SetProperty(ref _workingDirectory, value);
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
        ExecutionStatus.Success => "Успешно завершено",
        ExecutionStatus.Failed => _exitCode.HasValue ? $"Завершено с ошибкой (код {_exitCode})" : "Завершено с ошибкой",
        ExecutionStatus.Cancelled => "Остановлено пользователем",
        _ => "Готово"
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

    public ICommand StopCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand CloseCommand { get; }

    public event Action? RequestScrollToEnd;

    public ExecutionConsoleViewModel(IProcessService processService)
    {
        _processService = processService;

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
    }

    public void PrepareForExecution(ScriptModel script)
    {
        _logBuffer.Clear();
        LogText = "";
        ScriptTitle = script.Title;
        ScriptType = script.ScriptType.ToString();
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
    }
}
