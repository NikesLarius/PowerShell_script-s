using System.Windows;
using Wpf.Ui.Controls;

namespace ScriptHub.Views;

public partial class ConfirmDeleteDialog : FluentWindow
{
    public bool DeletePhysicalFile => DeleteFileCheckBox.IsChecked == true;

    public ConfirmDeleteDialog(string scriptTitle)
    {
        InitializeComponent();
        MessageTextBlock.Text = $"Вы действительно хотите удалить скрипт «{scriptTitle}» из Script Hub?";
    }

    private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
