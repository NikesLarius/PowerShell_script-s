using System.Windows.Controls;
using System.Windows.Input;
using ScriptHub.ViewModels;

namespace ScriptHub.Views;

public partial class ExecutionConsoleView : UserControl
{
    public ExecutionConsoleView()
    {
        InitializeComponent();
        DataContextChanged += ExecutionConsoleView_DataContextChanged;
    }

    private void ExecutionConsoleView_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ExecutionConsoleViewModel oldVm)
        {
            oldVm.RequestScrollToEnd -= OnScrollToEnd;
            oldVm.RequestFocusInput -= OnFocusInput;
        }

        if (e.NewValue is ExecutionConsoleViewModel newVm)
        {
            newVm.RequestScrollToEnd += OnScrollToEnd;
            newVm.RequestFocusInput += OnFocusInput;
        }
    }

    private void OnScrollToEnd()
    {
        Dispatcher.InvokeAsync(() =>
        {
            TerminalTextBox.ScrollToEnd();
        });
    }

    private void OnFocusInput()
    {
        Dispatcher.InvokeAsync(() =>
        {
            CommandInputBox.Focus();
            CommandInputBox.SelectAll();
        });
    }

    private void CommandInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ExecutionConsoleViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            if (vm.SendCommand.CanExecute(null))
            {
                vm.SendCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up)
        {
            if (vm.HistoryPrevCommand.CanExecute(null))
            {
                vm.HistoryPrevCommand.Execute(null);
                CommandInputBox.CaretIndex = CommandInputBox.Text.Length;
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            if (vm.HistoryNextCommand.CanExecute(null))
            {
                vm.HistoryNextCommand.Execute(null);
                CommandInputBox.CaretIndex = CommandInputBox.Text.Length;
            }
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
        {
            if (vm.IsRunning && vm.StopCommand.CanExecute(null))
            {
                vm.StopCommand.Execute(null);
                e.Handled = true;
            }
        }
    }
}
