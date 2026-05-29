using Microsoft.Extensions.DependencyInjection;
using WSM.Core.Interfaces;
using WSM.Core.Services;
using WSM.Infrastructure.Logging;
using WSM.Infrastructure.Paths;
using WSM.Infrastructure.Persistence;
using WSM.Infrastructure.Scm;
using WSM.Infrastructure.WinSw;

namespace WSM.Infrastructure.DependencyInjection;

/// <summary>
/// 基础设施层依赖注入扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWsmInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<WsmPaths>();
        services.AddSingleton<WinSwCliExecutor>();
        services.AddSingleton<WindowsScmService>();
        services.AddSingleton<ServiceLogReader>();
        services.AddSingleton<IWinSwConfigGenerator, WinSwXmlGenerator>();
        services.AddSingleton<IServiceRepository, SqliteServiceRepository>();
        services.AddSingleton<IWinSwHostService, WinSwHostService>();

        return services;
    }
}
