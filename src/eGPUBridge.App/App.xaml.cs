using System.Windows;
using eGPUBridge.App.Services;

namespace eGPUBridge.App;

public partial class App : System.Windows.Application
{
    private MainWindow? _mainWindow;
    private TrayIconService? _trayIcon;
    private AppLogger? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _logger = new AppLogger();
        _logger.Info("application.started", "eGPUBridge started.", new
        {
            version = typeof(App).Assembly.GetName().Version?.ToString(),
            os = Environment.OSVersion.VersionString
        });

        var displayService = new WindowsDisplayService(_logger);
        _mainWindow = new MainWindow(displayService, _logger);
        _trayIcon = new TrayIconService();
        _trayIcon.ShowRequested += ShowMainWindow;
        _trayIcon.ExitRequested += ExitApplication;
        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _logger?.Info("application.stopped", "eGPUBridge stopped.");
        base.OnExit(e);
    }

    private void ShowMainWindow()
    {
        if (_mainWindow is null)
        {
            return;
        }

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        _mainWindow?.PrepareForExit();
        Shutdown();
    }
}

