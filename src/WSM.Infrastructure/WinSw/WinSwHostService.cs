using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using WSM.Core.Interfaces;
using WSM.Core.Models;
using WSM.Infrastructure.Paths;
using WSM.Infrastructure.Scm;
using WSM.Infrastructure.Security;
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
    private readonly IOperationLogSink? _operationLog;

    public WinSwHostService(
        WsmPaths paths,
        WinSwCliExecutor cliExecutor,
        WindowsScmService scmService,
        IWinSwConfigGenerator configGenerator,
        IServiceRepository serviceRepository,
        IOperationLogSink? operationLog = null)
    {
        _paths = paths;
        _cliExecutor = cliExecutor;
        _scmService = scmService;
        _configGenerator = configGenerator;
        _serviceRepository = serviceRepository;
        _operationLog = operationLog;
    }

    public async Task<OperationResult> InstallAsync(ManagedService service, CancellationToken cancellationToken = default)
    {
        if (service == null)
        {
            throw new ArgumentNullException(nameof(service));
        }

        LogInstallDetail($"开始安装服务「{service.DisplayName}」...");
        LogOperation(OperationLogLevel.Info, "安装", $"开始安装服务「{service.DisplayName}」({service.Id})");

        try
        {
            if (!AdminHelper.IsRunningAsAdministrator())
            {
                const string message = "安装 Windows 服务需要管理员权限。请确认 UAC 提权，或在设置中点击「以管理员身份重启」。";
                LogInstallDetail("[错误] " + message);
                LogOperation(OperationLogLevel.Error, "安装", message);
                return OperationResult.Fail(message, errorCode: "ADMIN_REQUIRED");
            }

            LogInstallDetail("[1/5] 准备数据目录...");
            _paths.EnsureLayout();

            LogInstallDetail("[2/5] 部署 WinSW 包装器与配置文件...");
            DeployServiceFiles(service);
            LogInstallDetail($"  包装器: {_paths.GetServiceWrapperExePath(service.Id)}");
            LogInstallDetail($"  配置文件: {_paths.GetServiceConfigPath(service.Id)}");
            LogInstallDetail($"  目标程序: {service.ExecutablePath}");

            var wrapperExe = _paths.GetServiceWrapperExePath(service.Id);
            LogInstallDetail("[3/5] 执行 WinSW install...");
            var installResult = await _cliExecutor.ExecuteAsync(
                    wrapperExe,
                    "install",
                    cancellationToken,
                    LogInstallDetail)
                .ConfigureAwait(false);

            if (!installResult.Success)
            {
                var message = BuildFailureMessage("安装服务失败", installResult);
                LogInstallDetail("[失败] " + message);
                LogOperation(OperationLogLevel.Error, "安装", message);
                var failResult = OperationResult.Fail(message, errorCode: "WINSW_INSTALL_FAILED");
                failResult.Details = BuildDetails(installResult);
                return failResult;
            }

            LogInstallDetail("[4/5] 保存服务配置到数据库...");
            service.CreatedAt = DateTime.UtcNow;
            service.UpdatedAt = service.CreatedAt;
            await _serviceRepository.SaveAsync(service, cancellationToken).ConfigureAwait(false);

            if (service.StartAfterInstall)
            {
                LogInstallDetail("[5/5] 安装后启动服务...");
                var startResult = await StartAsync(service.Id, cancellationToken).ConfigureAwait(false);
                if (!startResult.Success)
                {
                    var warning = $"服务已安装，但启动失败：{startResult.Message}";
                    LogInstallDetail("[警告] " + warning);
                    LogOperation(OperationLogLevel.Warning, "安装", warning);
                    LogInstallDetail("安装流程结束（部分成功）。");
                    var partialResult = OperationResult.Ok(warning);
                    partialResult.Details = startResult.Details;
                    return partialResult;
                }

                LogInstallDetail("服务已成功启动。");
            }
            else
            {
                LogInstallDetail("[5/5] 跳过安装后启动。");
            }

            var successMessage = $"服务「{service.DisplayName}」安装成功。";
            LogInstallDetail("[完成] " + successMessage);
            LogOperation(OperationLogLevel.Success, "安装", successMessage);
            return OperationResult.Ok(successMessage);
        }
        catch (Exception ex)
        {
            var message = "安装服务时发生异常：" + ex.Message;
            LogInstallDetail("[异常] " + message);
            LogOperation(OperationLogLevel.Error, "安装", message);
            return OperationResult.Fail(message, ex, "WINSW_INSTALL_EXCEPTION");
        }
    }

    public async Task<OperationResult> UninstallAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        LogOperation(OperationLogLevel.Info, "卸载", $"开始卸载服务「{serviceId}」");

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
                    var message = BuildFailureMessage("卸载服务失败", uninstallResult);
                    LogOperation(OperationLogLevel.Error, "卸载", message);
                    var failResult = OperationResult.Fail(message, errorCode: "WINSW_UNINSTALL_FAILED");
                    failResult.Details = BuildDetails(uninstallResult);
                    return failResult;
                }
            }

            await _serviceRepository.DeleteAsync(serviceId, cancellationToken).ConfigureAwait(false);
            var success = $"服务「{serviceId}」已卸载。";
            LogOperation(OperationLogLevel.Success, "卸载", success);
            return OperationResult.Ok(success);
        }
        catch (Exception ex)
        {
            var message = "卸载服务时发生异常：" + ex.Message;
            LogOperation(OperationLogLevel.Error, "卸载", message);
            return OperationResult.Fail(message, ex, "WINSW_UNINSTALL_EXCEPTION");
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

    public async Task<ServiceRuntimeInfo> GetRuntimeInfoAsync(string serviceId, CancellationToken cancellationToken = default)
    {
        var status = await GetStatusAsync(serviceId, cancellationToken).ConfigureAwait(false);
        var info = new ServiceRuntimeInfo { Status = status };

        if (status == ServiceRuntimeStatus.Running)
        {
            info.StartedAt = _scmService.GetServiceStartedAt(serviceId);
        }

        return info;
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

        LogOperation(OperationLogLevel.Info, "刷新", $"刷新服务配置「{service.DisplayName}」");

        try
        {
            DeployServiceFiles(service);

            var wrapperExe = _paths.GetServiceWrapperExePath(service.Id);
            var refreshResult = await _cliExecutor.ExecuteAsync(wrapperExe, "refresh", cancellationToken)
                .ConfigureAwait(false);

            if (!refreshResult.Success)
            {
                var message = BuildFailureMessage("刷新服务配置失败", refreshResult);
                LogOperation(OperationLogLevel.Error, "刷新", message);
                var failResult = OperationResult.Fail(message, errorCode: "WINSW_REFRESH_FAILED");
                failResult.Details = BuildDetails(refreshResult);
                return failResult;
            }

            service.UpdatedAt = DateTime.UtcNow;
            await _serviceRepository.SaveAsync(service, cancellationToken).ConfigureAwait(false);
            var success = $"服务「{service.DisplayName}」配置已刷新。";
            LogOperation(OperationLogLevel.Success, "刷新", success);
            return OperationResult.Ok(success);
        }
        catch (Exception ex)
        {
            var message = "刷新服务配置时发生异常：" + ex.Message;
            LogOperation(OperationLogLevel.Error, "刷新", message);
            return OperationResult.Fail(message, ex, "WINSW_REFRESH_EXCEPTION");
        }
    }

    private async Task<OperationResult> ExecuteLifecycleCommandAsync(
        string serviceId,
        string command,
        string actionName,
        CancellationToken cancellationToken)
    {
        LogOperation(OperationLogLevel.Info, actionName, $"正在{actionName}服务「{serviceId}」");

        try
        {
            var wrapperExe = _paths.GetServiceWrapperExePath(serviceId);
            if (!File.Exists(wrapperExe))
            {
                var message = $"服务「{serviceId}」尚未安装。";
                LogOperation(OperationLogLevel.Error, actionName, message);
                return OperationResult.Fail(message, errorCode: "SERVICE_NOT_INSTALLED");
            }

            var result = await _cliExecutor.ExecuteAsync(wrapperExe, command, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                var message = BuildFailureMessage($"{actionName}服务失败", result);
                LogOperation(OperationLogLevel.Error, actionName, message);
                var failResult = OperationResult.Fail(message, errorCode: "WINSW_COMMAND_FAILED");
                failResult.Details = BuildDetails(result);
                return failResult;
            }

            var success = $"服务「{serviceId}」{actionName}成功。";
            LogOperation(OperationLogLevel.Success, actionName, success);
            return OperationResult.Ok(success);
        }
        catch (Exception ex)
        {
            var message = $"{actionName}服务时发生异常：" + ex.Message;
            LogOperation(OperationLogLevel.Error, actionName, message);
            return OperationResult.Fail(message, ex, "WINSW_COMMAND_EXCEPTION");
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

        var xmlBytes = _configGenerator.GenerateUtf8Bytes(service);
        File.WriteAllBytes(_paths.GetServiceConfigPath(service.Id), xmlBytes);
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

    private void LogInstallDetail(string line)
    {
        LogOperation(OperationLogLevel.Info, "WinSW", line);
    }

    private void LogOperation(OperationLogLevel level, string category, string message)
    {
        _operationLog?.Log(level, category, message);
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

    private static string BuildDetails(WinSwCommandResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.StandardOutput)
            && !string.IsNullOrWhiteSpace(result.StandardError))
        {
            return result.StandardOutput + Environment.NewLine + result.StandardError;
        }

        return result.StandardOutput + result.StandardError;
    }
}
