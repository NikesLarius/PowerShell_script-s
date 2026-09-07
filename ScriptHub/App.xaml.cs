using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ScriptHub.Models;
using ScriptHub.Services;
using ScriptHub.Services.Contracts;
using ScriptHub.ViewModels;

namespace ScriptHub;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    [STAThread]
    public static void Main()
    {
        var app = new App();
        app.InitializeComponent();

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScriptHub", "crash.log");
            try
            {
                File.WriteAllText(logPath, $"{DateTime.Now}: {ex?.Message}\n{ex?.StackTrace}\n{ex?.InnerException}");
            }
            catch { }
            MessageBox.Show($"Критическая ошибка: {ex?.Message}\n{ex?.StackTrace}", "Script Hub Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        app.DispatcherUnhandledException += (s, args) =>
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScriptHub", "crash.log");
            try
            {
                File.WriteAllText(logPath, $"{DateTime.Now}: {args.Exception?.Message}\n{args.Exception?.StackTrace}\n{args.Exception?.InnerException}");
            }
            catch { }
            MessageBox.Show($"Ошибка приложения: {args.Exception?.Message}\n{args.Exception?.StackTrace}", "Script Hub Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        try
        {
            var services = new ServiceCollection();
            app.ConfigureServices(services);
            app._serviceProvider = services.BuildServiceProvider();

            // 1. Initialize storage and seed data
            var scriptService = app._serviceProvider.GetRequiredService<ScriptService>();
            scriptService.InitializeAsync().GetAwaiter().GetResult();

            // 2. Setup ViewModel
            var mainViewModel = app._serviceProvider.GetRequiredService<MainViewModel>();
            mainViewModel.InitializeAsync().GetAwaiter().GetResult();

            // 3. Setup MainWindow and Theme
            var mainWindow = app._serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.DataContext = mainViewModel;

            var config = scriptService.GetConfig();
            mainWindow.ApplyTheme(config.Theme);

            app.Run(mainWindow);
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScriptHub", "startup_crash.log");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                File.WriteAllText(logPath, $"{DateTime.Now}: {ex.Message}\n{ex.StackTrace}\n{ex.InnerException}");
            }
            catch { }
            MessageBox.Show($"Ошибка запуска Script Hub: {ex.Message}\n{ex.StackTrace}", "Script Hub Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Services
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<ScriptService>();
        services.AddSingleton<IScriptService>(sp => sp.GetRequiredService<ScriptService>());
        services.AddSingleton<IProcessService, ProcessService>();
        services.AddSingleton<IHistoryService, HistoryService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IDialogService, DialogService>();

        // ViewModels
        services.AddSingleton<MainViewModel>(sp =>
        {
            var scriptService = sp.GetRequiredService<IScriptService>();
            var processService = sp.GetRequiredService<IProcessService>();
            var historyService = sp.GetRequiredService<IHistoryService>();
            var dialogService = sp.GetRequiredService<IDialogService>();
            var backupService = sp.GetRequiredService<IBackupService>();

            return new MainViewModel(
                scriptService,
                processService,
                historyService,
                dialogService,
                backupService,
                themeMode =>
                {
                    if (Current?.MainWindow is MainWindow mw)
                    {
                        mw.ApplyTheme(themeMode);
                    }
                });
        });

        // Views
        services.AddSingleton<MainWindow>();
    }
}
