using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ScriptHub.Controls;
using ScriptHub.ViewModels;

namespace ScriptHub.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var original = e.OriginalSource as DependencyObject;
        while (original != null && original != sender)
        {
            if (original is ScriptTileControl || original is ButtonBase || original is TextBox || original is ComboBox)
            {
                return;
            }
            original = VisualTreeHelper.GetParent(original);
        }

        if (DataContext is MainViewModel vm)
        {
            vm.CollapseAllTiles();
        }
    }

    private void CategoryFilter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string categoryId && DataContext is MainViewModel vm)
        {
            vm.SelectedCategoryFilterId = categoryId;
        }
    }

    private void UserControl_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void UserControl_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && DataContext is MainViewModel vm)
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            vm.HandleFileDrop(files);
            e.Handled = true;
        }
    }
}
