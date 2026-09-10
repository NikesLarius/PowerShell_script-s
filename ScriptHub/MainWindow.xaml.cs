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
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const uint LWA_ALPHA = 0x00000002;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern IntPtr GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    private static IntPtr GetWindowLong(IntPtr hWnd, int nIndex)
    {
        return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLong32(hWnd, nIndex);
    }

    private static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
    }

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

        var opacity = ViewModel?.SettingsVM.WindowOpacity ?? 1.0;
        ApplyOpacity(opacity);
    }

    public void ApplyOpacity(double opacity)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => ApplyOpacity(opacity));
            return;
        }

        var clamped = Math.Clamp(opacity, 0.2, 1.0);

        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                long style = GetWindowLong(hwnd, GWL_EXSTYLE).ToInt64();

                if (Math.Abs(clamped - 1.0) < 0.001)
                {
                    // 100% Opacity - restore Mica backdrop
                    if (WindowBackdropType != WindowBackdropType.Mica)
                    {
                        WindowBackdropType = WindowBackdropType.Mica;
                    }

                    if ((style & WS_EX_LAYERED) != 0)
                    {
                        SetLayeredWindowAttributes(hwnd, 0, 255, LWA_ALPHA);
                    }
                }
                else
                {
                    // < 100% Opacity - switch backdrop to None so DWM allows alpha transparency
                    if (WindowBackdropType != WindowBackdropType.None)
                    {
                        WindowBackdropType = WindowBackdropType.None;
                    }

                    if ((style & WS_EX_LAYERED) == 0)
                    {
                        SetWindowLong(hwnd, GWL_EXSTYLE, new IntPtr(style | WS_EX_LAYERED));
                        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                    }

                    byte alpha = (byte)Math.Round(clamped * 255.0);
                    SetLayeredWindowAttributes(hwnd, 0, alpha, LWA_ALPHA);
                }
            }
        }
        catch { }
    }

    public void ApplyTheme(ThemeMode themeMode)
    {
        var opacity = ViewModel?.SettingsVM.WindowOpacity ?? 1.0;
        var backdrop = opacity < 0.99 ? WindowBackdropType.None : WindowBackdropType.Mica;

        switch (themeMode)
        {
            case ThemeMode.Light:
                try { SystemThemeWatcher.UnWatch(this); } catch { }
                ApplicationThemeManager.Apply(ApplicationTheme.Light, backdrop, true);
                break;

            case ThemeMode.Dark:
                try { SystemThemeWatcher.UnWatch(this); } catch { }
                ApplicationThemeManager.Apply(ApplicationTheme.Dark, backdrop, true);
                break;

            case ThemeMode.System:
            default:
                try { SystemThemeWatcher.Watch(this, backdrop, true); } catch { }
                var isDark = ApplicationThemeManager.IsMatchedDark();
                ApplicationThemeManager.Apply(isDark ? ApplicationTheme.Dark : ApplicationTheme.Light, backdrop, true);
                break;
        }

        ApplicationAccentColorManager.ApplySystemAccent();
        ApplyOpacity(opacity);
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

        // Delete -> Delete script
        if (e.Key == Key.Delete && !ViewModel.IsEditorViewActive && !SearchBox.IsFocused)
        {
            ViewModel.DeleteSelectedCommand.Execute(null);
            e.Handled = true;
            return;
        }
    }
}
