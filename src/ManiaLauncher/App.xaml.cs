using System.Windows;
using System.Windows.Threading;
using ManiaLauncher.Services;

namespace ManiaLauncher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        AppInfo.Init();
        LogService.Instance.Info($"=== {AppInfo.Brand} v{AppInfo.Version} starting (pid={Environment.ProcessId}) ===");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        LogService.Instance.Info($"Exiting with code {e.ApplicationExitCode}");
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogService.Instance.Fatal($"Unhandled UI exception: {e.Exception}");
        MessageBox.Show(
            "Произошла непредвиденная ошибка:\n\n" + e.Exception.Message +
            "\n\nПодробности записаны в файл журнала.",
            AppInfo.Title + " — Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            LogService.Instance.Fatal($"Unhandled exception: {ex}");
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        LogService.Instance.Fatal($"Unobserved task exception: {e.Exception}");
        e.SetObserved();
    }
}
