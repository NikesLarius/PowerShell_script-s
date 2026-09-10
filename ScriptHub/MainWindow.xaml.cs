using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ScriptHub.Models;
using ScriptHub.ViewModels;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ScriptHub;

public partial class MainWindow : FluentWindow
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var theme = ViewModel?.SettingsVM.SelectedTheme ?? ThemeMode.System;
        ApplyTheme(theme);
    }

    public void ApplyTheme(ThemeMode themeMode)
    {
        switch (themeMode)
        {
            case ThemeMode.Light:
                try { SystemThemeWatcher.UnWatch(this); } catch { }
                ApplicationThemeManager.Apply(ApplicationTheme.Light, WindowBackdropType.Mica, true);
                break;

            case ThemeMode.Dark:
                try { SystemThemeWatcher.UnWatch(this); } catch { }
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, WindowBackdropType.Mica, true);
                break;

            case ThemeMode.System:
            default:
                try { SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, true); } catch { }
                var isDark = ApplicationThemeManager.IsMatchedDark();
                ApplicationThemeManager.Apply(isDark ? ApplicationTheme.Dark : ApplicationTheme.Light, WindowBackdropType.Mica, true);
                break;
        }

        try
        {
            ApplicationAccentColorManager.ApplySystemAccent();
        }
        catch { }
    }

    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                try
                {
                    DragMove();
                }
                catch { }
            }
        }
    }

    private void FluentWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (ViewModel == null) return;

        // Ctrl + F -> Focus search
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
            return;
        }

        // Ctrl + N -> New script
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.N)
        {
            ViewModel.ShowAddScriptCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Ctrl + O -> Import script
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.O)
        {
            ViewModel.ShowAddScriptCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Ctrl + , -> Settings
        if (Keyboard.Modifiers == ModifierKeys.Control && (e.Key == Key.OemComma || e.Key == Key.OemPeriod))
        {
            ViewModel.NavigateCommand.Execute("Settings");
            e.Handled = true;
            return;
        }

        // F5 -> Run selected script (or Save & Run if editor active)
        if (e.Key == Key.F5)
        {
            if (ViewModel.IsEditorViewActive)
            {
                ViewModel.EditorVM.SaveAndRunCommand.Execute(null);
            }
            else
            {
                ViewModel.RunSelectedScriptCommand.Execute(null);
            }
            e.Handled = true;
            return;
        }

        // Ctrl + S -> Save in Editor
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.S)
        {
            if (ViewModel.IsEditorViewActive)
            {
                ViewModel.EditorVM.SaveCommand.Execute(null);
                e.Handled = true;
                return;
            }
        }

        // Ctrl + ~ or F12 -> Toggle Interactive Terminal
        if ((Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.OemTilde) || e.Key == Key.F12)
        {
            ViewModel.ToggleTerminalCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Delete -> Delete script
        if (e.Key == Key.Delete && !ViewModel.IsEditorViewActive && !SearchBox.IsFocused)
        {
            ViewModel.DeleteSelectedCommand.Execute(null);
            e.Handled = true;
            return;
        }
    }
}
