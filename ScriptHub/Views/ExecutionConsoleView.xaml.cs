using System.Windows.Controls;
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
        }

        if (e.NewValue is ExecutionConsoleViewModel newVm)
        {
            newVm.RequestScrollToEnd += OnScrollToEnd;
        }
    }

    private void OnScrollToEnd()
    {
        Dispatcher.InvokeAsync(() =>
        {
            TerminalTextBox.ScrollToEnd();
        });
    }
}
