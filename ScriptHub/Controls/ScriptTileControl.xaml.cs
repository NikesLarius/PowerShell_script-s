using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ScriptHub.ViewModels;

namespace ScriptHub.Controls;

public partial class ScriptTileControl : UserControl
{
    public ScriptTileControl()
    {
        InitializeComponent();
    }

    private void Card_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var original = e.OriginalSource as DependencyObject;
        while (original != null && original != sender)
        {
            if (original is ButtonBase || original is TextBox || original is MenuItem || original is ContextMenu)
            {
                return;
            }
            original = VisualTreeHelper.GetParent(original);
        }

        if (DataContext is ScriptTileViewModel vm)
        {
            vm.IsExpanded = !vm.IsExpanded;
        }
    }

    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement button && button.ContextMenu != null)
        {
            button.ContextMenu.PlacementTarget = button;
            button.ContextMenu.Placement = PlacementMode.Bottom;
            button.ContextMenu.IsOpen = true;
        }
    }
}
