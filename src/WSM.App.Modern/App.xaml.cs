using System.Windows;
using WSM.App.Shared.Hosting;

namespace WSM.App.Modern;

public partial class App : Application
{
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _host = new AppHost();
        MainWindow = _host.CreateMainWindow();
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Shutdown();
        base.OnExit(e);
    }
}
