using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace ScriptHub.Views;

public enum MessageDialogType
{
    Information,
    Success,
    Error,
    Question
}

public partial class MessageDialog : FluentWindow
{
    public bool Confirmed { get; private set; }

    public MessageDialog(
        string title, 
        string message, 
        MessageDialogType type = MessageDialogType.Information,
        string primaryText = "OK",
        string? secondaryText = null)
    {
        InitializeComponent();

        Title = title;
        TitleTextBlock.Text = title;
        MessageTextBlock.Text = message;
        PrimaryButton.Content = primaryText;

        if (!string.IsNullOrWhiteSpace(secondaryText))
        {
            SecondaryButton.Content = secondaryText;
            SecondaryButton.Visibility = Visibility.Visible;
        }
        else
        {
            SecondaryButton.Visibility = Visibility.Collapsed;
        }

        ConfigureType(type);
    }

    private void ConfigureType(MessageDialogType type)
    {
        switch (type)
        {
            case MessageDialogType.Success:
                DialogSymbolIcon.Symbol = SymbolRegular.CheckmarkCircle24;
                DialogSymbolIcon.Foreground = new SolidColorBrush(Color.FromRgb(16, 124, 65)); // Success Green
                break;

            case MessageDialogType.Error:
                DialogSymbolIcon.Symbol = SymbolRegular.DismissCircle24;
                DialogSymbolIcon.Foreground = new SolidColorBrush(Color.FromRgb(209, 52, 56)); // Error Red
                PrimaryButton.Appearance = ControlAppearance.Danger;
                break;

            case MessageDialogType.Question:
                DialogSymbolIcon.Symbol = SymbolRegular.QuestionCircle24;
                DialogSymbolIcon.Foreground = (Brush)FindResource("AccentFillColorDefaultBrush");
                break;

            case MessageDialogType.Information:
            default:
                DialogSymbolIcon.Symbol = SymbolRegular.Info24;
                DialogSymbolIcon.Foreground = (Brush)FindResource("AccentFillColorDefaultBrush");
                break;
        }
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        DialogResult = true;
        Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        DialogResult = false;
        Close();
    }
}
