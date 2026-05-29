using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WSM.Core.Interfaces;
using WSM.Core.Models;
using WSM.Infrastructure.Paths;
using WSM.Infrastructure.Scm;
using WSM.Infrastructure.WinSw;

namespace WSM.Infrastructure.WinSw;

/// <summary>
/// WinSW 服务宿主实现。
/// </summary>
public sealed class WinSwHostService : IWinSwHostService
{
    private readonly WsmPaths _paths;
    private readonly WinSwCliExecutor _cliExecutor;
    private readonly WindowsScmService _scmService;
    private readonly IWinSwConfigGenerator _configGenerator;
    private readonly IServiceRepository _serviceRepository;

    public WinSwHostService(
        WsmPaths paths,
        WinSwCliExecutor cliExecutor,
        WindowsScmService scmService,
        IWinSwConfigGenerator configGenerator,
        IServiceRepository serviceRepository)
    {
        _paths = paths;
        _cliExecutor = cliExecutor;
        _scmService = scmService;
        _configGenerator = configGenerator;
        _serviceRepository = serviceRepository;
    }

    public async Task<OperationResult> InstallAsync(ManagedService service, CancellationToken cancellationToken = default)
    {
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }

        try
        {
            _paths.EnsureLayout();
            DeployServiceFiles(service);

            var wrapperExe = _paths.GetServiceWrapperExePath(service.Id);
            var installResult = await _cliExecutor.ExecuteAsync(wrapperExe, "install", cancellationToken)
                .ConfigureAwait(false);

            if (!installResult.Success)
            {
                return OperationResult.Fail(
                    BuildFailureMessage("安装服务失败", installResult),
                    errorCode: "WINSW_INSTALL_FAILED");
            }

            service.CreatedAt = DateTime.UtcNow;
            service.UpdatedAt = service.CreatedAt;
            await _serviceRepository.SaveAsync(service, cancellationToken).ConfigureAwait(false);

            if (service.StartAfterInstall)
            {
                var startResult = await StartAsync(service.Id, cancellationToken).ConfigureAwait(false);
                if (!startResult.Success)
                {
                    return OperationResult.Fail(
                        "服务已安装，但启动失败：" + startResult.Message,
                        errorCode: "WINSW_START_FAILED");
                }
            }

            return OperationResult.Ok($"服务「{service.DisplayName}」安装成功。");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail("安装服务时发生异常：" + ex.Message, ex, "WINSW_INSTALL_EXCEPTION");
        }
    }

    public async Task<OperationResult> UninstallAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        try
        {
            var wrapperExe = _paths.GetServiceWrapperExePath(serviceId);
            if (File.Exists(wrapperExe))
            {
                if (_scmService.GetRuntimeStatus(serviceId) == ServiceRuntimeStatus.Running)
                {
                    await _cliExecutor.ExecuteAsync(wrapperExe, "stop", cancellationToken).ConfigureAwait(false);
                }

                var uninstallResult = await _cliExecutor.ExecuteAsync(wrapperExe, "uninstall", cancellationToken)
                    .ConfigureAwait(false);

                if (!uninstallResult.Success)
                {
                    return OperationResult.Fail(
                        BuildFailureMessage("卸载服务失败", uninstallResult),
                        errorCode: "WINSW_UNINSTALL_FAILED");
                }
            }

            await _serviceRepository.DeleteAsync(serviceId, cancellationToken).ConfigureAwait(false);
            return OperationResult.Ok($"服务「{serviceId}」已卸载。");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail("卸载服务时发生异常：" + ex.Message, ex, "WINSW_UNINSTALL_EXCEPTION");
        }
    }

    public async Task<ServiceRuntimeStatus> GetStatusAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var wrapperExe = _paths.GetServiceWrapperExePath(serviceId);
        if (!File.Exists(wrapperExe))
        {
            return ServiceRuntimeStatus.NotInstalled;
        }

        var cliResult = await _cliExecutor.ExecuteAsync(wrapperExe, "status", cancellationToken)
            .ConfigureAwait(false);

        var cliStatus = WinSwCliExecutor.ParseStatusOutput(cliResult.StandardOutput);
        switch (cliStatus)
        {
            case "Started":
                return ServiceRuntimeStatus.Running;
            case "Stopped":
                return ServiceRuntimeStatus.Stopped;
            case "NonExistent":
                return ServiceRuntimeStatus.NotInstalled;
        }

        return _scmService.GetRuntimeStatus(serviceId);
    }

    public async Task<OperationResult> StartAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        return await ExecuteLifecycleCommandAsync(serviceId, "start", "启动", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OperationResult> StopAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        return await ExecuteLifecycleCommandAsync(serviceId, "stop", "停止", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OperationResult> RestartAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        return await ExecuteLifecycleCommandAsync(serviceId, "restart", "重启", cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<OperationResult> RefreshAsync(ManagedService service, CancellationToken cancellationToken = default)
    {
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }

        try
        {
            DeployServiceFiles(service);

            var wrapperExe = _paths.GetServiceWrapperExePath(service.Id);
            var refreshResult = await _cliExecutor.ExecuteAsync(wrapperExe, "refresh", cancellationToken)
                .ConfigureAwait(false);

            if (!refreshResult.Success)
            {
                return OperationResult.Fail(
                    BuildFailureMessage("刷新服务配置失败", refreshResult),
                    errorCode: "WINSW_REFRESH_FAILED");
            }

            service.UpdatedAt = DateTime.UtcNow;
            await _serviceRepository.SaveAsync(service, cancellationToken).ConfigureAwait(false);
            return OperationResult.Ok($"服务「{service.DisplayName}」配置已刷新。");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail("刷新服务配置时发生异常：" + ex.Message, ex, "WINSW_REFRESH_EXCEPTION");
        }
    }

    private async Task<OperationResult> ExecuteLifecycleCommandAsync(
        string serviceId,
        string command,
        string actionName,
        CancellationToken cancellationToken)
    {
        try
        {
            var wrapperExe = _paths.GetServiceWrapperExePath(serviceId);
            if (!File.Exists(wrapperExe))
            {
                return OperationResult.Fail($"服务「{serviceId}」尚未安装。", errorCode: "SERVICE_NOT_INSTALLED");
            }

            var result = await _cliExecutor.ExecuteAsync(wrapperExe, command, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                return OperationResult.Fail(
                    BuildFailureMessage($"{actionName}服务失败", result),
                    errorCode: "WINSW_COMMAND_FAILED");
            }

            return OperationResult.Ok($"服务「{serviceId}」{actionName}成功。");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"{actionName}服务时发生异常：" + ex.Message, ex, "WINSW_COMMAND_EXCEPTION");
        }
    }

    private void DeployServiceFiles(ManagedService service)
    {
        var serviceDirectory = _paths.GetServiceDirectory(service.Id);
        Directory.CreateDirectory(serviceDirectory);
        Directory.CreateDirectory(_paths.GetServiceLogsDirectory(service.Id));

        var sourceWinSw = ResolveWinSwSourcePath();
        var wrapperExe = _paths.GetServiceWrapperExePath(service.Id);
        File.Copy(sourceWinSw, wrapperExe, overwrite: true);

        var xml = _configGenerator.Generate(service);
        File.WriteAllText(_paths.GetServiceConfigPath(service.Id), xml);
    }

    private string ResolveWinSwSourcePath()
    {
        var bundled = _paths.GetBundledWinSwPath();
        if (File.Exists(bundled))
        {
            return bundled;
        }

        Directory.CreateDirectory(_paths.WinSwStoreDirectory);
        var stored = Path.Combine(_paths.WinSwStoreDirectory, "WinSW-x64.exe");
        if (File.Exists(stored))
        {
            return stored;
        }

        throw new FileNotFoundException(
            "未找到 WinSW 可执行文件。请将 WinSW-x64.exe 放入应用 winsw 目录或 %ProgramData%\\WSM\\winsw。");
    }

    private static string BuildFailureMessage(string prefix, WinSwCommandResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            return $"{prefix}：{result.StandardError}";
        }

        if (!string.IsNullOrWhiteSpace(result.StandardOutput))
        {
            return $"{prefix}：{result.StandardOutput}";
        }

        return $"{prefix}（退出码 {result.ExitCode}）。";
    }
}
