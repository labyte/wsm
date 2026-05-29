using Microsoft.Extensions.DependencyInjection;

namespace WSM.Infrastructure.DependencyInjection;

/// <summary>
/// 基础设施层依赖注入扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWsmInfrastructure(this IServiceCollection services)
    {
        // P2 阶段注册 WinSW、SCM、日志、SQLite 等服务
        return services;
    }
}
