using System.Windows;

namespace LyricNow;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Autostart plus a manual launch would otherwise give two widgets.
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\LyricNow.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        var backgroundMode = e.Args.Contains(Autostart.BackgroundArg, StringComparer.OrdinalIgnoreCase);
        var window = new MainWindow(backgroundMode);
        MainWindow = window;
        window.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
