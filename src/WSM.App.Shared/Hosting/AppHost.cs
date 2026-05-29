using System;
using Microsoft.Extensions.DependencyInjection;
using WSM.App.Shared.Services;
using WSM.App.Shared.ViewModels;
using WSM.App.Shared.Views;
using WSM.Core.Interfaces;
using WSM.Infrastructure.DependencyInjection;
using MaterialDesignThemes.Wpf;

namespace WSM.App.Shared.Hosting;

/// <summary>
/// 应用宿主：依赖注入与主窗口创建。
/// </summary>
public sealed class AppHost
{
    public AppHost()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();
    }

    public IServiceProvider Services { get; }

    public MainWindow CreateMainWindow()
    {
        var window = Services.GetRequiredService<MainWindow>();
        Services.GetRequiredService<ITrayIconService>().Attach(window);
        return window;
    }

    public void Shutdown()
    {
        if (Services.GetService<ITrayIconService>() is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(_ => new SnackbarMessageQueue(TimeSpan.FromSeconds(4)));
        services.AddSingleton<ISnackbarService, SnackbarService>();
        services.AddSingleton<ITrayIconService, TrayIconService>();
        services.AddSingleton<AdminElevationService>();
        services.AddSingleton<ConsoleLogHelper>();

        services.AddSingleton<AppOperationLogService>();
        services.AddSingleton<IOperationLogSink>(sp => sp.GetRequiredService<AppOperationLogService>());

        services.AddSingleton<ServiceListViewModel>();
        services.AddSingleton<ServiceInstallViewModel>();
        services.AddSingleton<ServiceConsoleViewModel>();
        services.AddSingleton<LogViewerViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<NavigationService>();
        services.AddSingleton<INavigationService>(sp => sp.GetRequiredService<NavigationService>());
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        services.AddWsmInfrastructure();
    }
}
