using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using WSM.Core.Interfaces;
using WSM.Infrastructure.DependencyInjection;
using WSM.Infrastructure.Paths;
using WSM.Infrastructure.Security;
using WSM.Infrastructure.WinSw;

namespace WSM.App.Shared.Cli;

/// <summary>
/// 供安装包卸载流程调用的命令行入口（无 UI）。
/// </summary>
public static class UninstallCliHandler
{
    public const int ExitNoServices = 0;
    public const int ExitPartialFailure = 1;
    public const int ExitAdminRequired = 2;
    public const int ExitFatalError = 255;
    public const int MaxReportedServiceCount = 254;

    /// <summary>
    /// 处理卸载相关命令行；返回 true 表示已处理并应直接退出进程。
    /// </summary>
    public static bool TryHandle(string[]? args, out int exitCode)
    {
        exitCode = ExitNoServices;
        var command = ResolveCommand(args);
        if (command == null)
        {
            return false;
        }

        switch (command)
        {
            case "--pre-uninstall-check":
                exitCode = RunPreUninstallCheck();
                return true;
            case "--uninstall-all-services":
                exitCode = RunUninstallAllServices();
                return true;
            default:
                return false;
        }
    }

    private static string? ResolveCommand(string[]? startupArgs)
    {
        if (startupArgs != null)
        {
            foreach (var arg in startupArgs)
            {
                if (IsCliCommand(arg))
                {
                    return arg.Trim().ToLowerInvariant();
                }
            }
        }

        foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
        {
            if (IsCliCommand(arg))
            {
                return arg.Trim().ToLowerInvariant();
            }
        }

        return null;
    }

    private static bool IsCliCommand(string? arg) =>
        !string.IsNullOrWhiteSpace(arg)
        && arg.StartsWith("--", StringComparison.Ordinal);

    /// <summary>
    /// 退出码：0=无托管服务，1-254=服务数量，255=检测失败。
    /// </summary>
    private static int RunPreUninstallCheck()
    {
        try
        {
            using var provider = CreateServiceProvider();
            var paths = provider.GetRequiredService<WsmPaths>();
            var repository = provider.GetRequiredService<IServiceRepository>();
            var serviceIds = CollectManagedServiceIds(paths, repository);
            WriteDiagnosticReport(paths, serviceIds);
            if (serviceIds.Count <= 0)
            {
                return ExitNoServices;
            }

            return Math.Min(serviceIds.Count, MaxReportedServiceCount);
        }
        catch (Exception ex)
        {
            WriteDiagnosticFailure(ex);
            return ExitFatalError;
        }
    }

    private static void WriteDiagnosticReport(WsmPaths paths, IReadOnlyList<string> serviceIds)
    {
        try
        {
            var reportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WSM",
                "pre-uninstall-check.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            var lines = new List<string>
            {
                $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"DataRoot: {paths.DataRoot}",
                $"ServicesDirectory: {paths.ServicesDirectory}",
                $"ServicesDirectoryExists: {Directory.Exists(paths.ServicesDirectory)}",
                $"ManagedServiceCount: {serviceIds.Count}",
            };
            lines.AddRange(serviceIds.Select(id => $"Service: {id}"));
            File.WriteAllLines(reportPath, lines);
        }
        catch
        {
            // 诊断报告写入失败不影响退出码。
        }
    }

    private static void WriteDiagnosticFailure(Exception ex)
    {
        try
        {
            var reportPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WSM",
                "pre-uninstall-check.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
            File.WriteAllText(reportPath, ex.ToString());
        }
        catch
        {
        }
    }

    /// <summary>
    /// 退出码：0=全部成功或无服务，1=部分失败，2=需要管理员，255=致命错误。
    /// </summary>
    private static int RunUninstallAllServices()
    {
        try
        {
            if (!AdminHelper.IsRunningAsAdministrator())
            {
                return ExitAdminRequired;
            }

            using var provider = CreateServiceProvider();
            var paths = provider.GetRequiredService<WsmPaths>();
            var repository = provider.GetRequiredService<IServiceRepository>();
            var host = provider.GetRequiredService<IWinSwHostService>();

            var serviceIds = CollectManagedServiceIds(paths, repository);
            if (serviceIds.Count == 0)
            {
                return ExitNoServices;
            }

            var failed = 0;
            foreach (var serviceId in serviceIds)
            {
                var result = host.UninstallAsync(serviceId).GetAwaiter().GetResult();
                if (!result.Success)
                {
                    failed++;
                }
            }

            return failed > 0 ? ExitPartialFailure : ExitNoServices;
        }
        catch
        {
            return ExitFatalError;
        }
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddWsmInfrastructure();
        return services.BuildServiceProvider();
    }

    private static int CountManagedServices(WsmPaths paths, IServiceRepository repository)
    {
        return CollectManagedServiceIds(paths, repository).Count;
    }

    private static List<string> CollectManagedServiceIds(WsmPaths paths, IServiceRepository repository)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var service in repository.GetAllAsync().GetAwaiter().GetResult())
        {
            if (!string.IsNullOrWhiteSpace(service.Id))
            {
                ids.Add(service.Id);
            }
        }

        if (!Directory.Exists(paths.ServicesDirectory))
        {
            return ids.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        foreach (var directory in Directory.GetDirectories(paths.ServicesDirectory))
        {
            var serviceId = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(serviceId))
            {
                continue;
            }

            var configPath = paths.GetServiceConfigPath(serviceId);
            if (File.Exists(configPath))
            {
                ids.Add(serviceId);
            }
        }

        return ids.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
