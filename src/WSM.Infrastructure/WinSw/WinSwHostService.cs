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
            LogInstallDetail($"  配置文件: {_paths.GetServiceConfigPath(service.Id)}");
            LogInstallDetail($"  目标程序: {service.ExecutablePath}");

            LogInstallDetail("[3/5] 执行 WinSW install...");
            var installResult = await ExecuteWinSwCommandAsync(
                    service.Id,
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
            var configFile = _paths.GetServiceConfigPath(serviceId);
            if (File.Exists(configFile))
            {
                if (_scmService.GetRuntimeStatus(serviceId) == ServiceRuntimeStatus.Running)
                {
                    await ExecuteWinSwCommandAsync(serviceId, "stop", cancellationToken).ConfigureAwait(false);
                }

                var uninstallResult = await ExecuteWinSwCommandAsync(serviceId, "uninstall", cancellationToken)
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
        var configFile = _paths.GetServiceConfigPath(serviceId);
        if (!File.Exists(configFile))
        {
            return ServiceRuntimeStatus.NotInstalled;
        }

        var cliResult = await ExecuteWinSwCommandAsync(serviceId, "status", cancellationToken)
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
            var configFile = _paths.GetServiceConfigPath(service.Id);
            if (!File.Exists(configFile))
            {
                var notInstalledMessage = $"服务「{service.DisplayName}」尚未安装，无法刷新配置。";
                LogOperation(OperationLogLevel.Error, "刷新", notInstalledMessage);
                return OperationResult.Fail(notInstalledMessage, errorCode: "SERVICE_NOT_INSTALLED");
            }

            // 刷新仅更新配置文件，避免在服务运行时覆盖包装器导致“文件被占用”。
            WriteServiceConfigFile(service);
            var refreshResult = await ExecuteWinSwCommandAsync(service.Id, "refresh", cancellationToken)
                .ConfigureAwait(false);

            if (!refreshResult.Success)
            {
                if (IsRefreshCommandUnsupported(refreshResult))
                {
                    service.UpdatedAt = DateTime.UtcNow;
                    await _serviceRepository.SaveAsync(service, cancellationToken).ConfigureAwait(false);
                    var fallbackMessage = $"服务「{service.DisplayName}」配置已保存。当前 WinSW 版本不支持 refresh 命令，请重启服务使配置生效。";
                    LogOperation(OperationLogLevel.Warning, "刷新", fallbackMessage);
                    return OperationResult.Ok(fallbackMessage);
                }

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
        catch (IOException ioEx)
        {
            var message = "刷新配置失败：配置文件正在被占用，请先停止服务后重试。";
            LogOperation(OperationLogLevel.Error, "刷新", message + " " + ioEx.Message);
            return OperationResult.Fail(message, ioEx, "WINSW_REFRESH_FILE_IN_USE");
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
            var configFile = _paths.GetServiceConfigPath(serviceId);
            if (!File.Exists(configFile))
            {
                var message = $"服务「{serviceId}」尚未安装。";
                LogOperation(OperationLogLevel.Error, actionName, message);
                return OperationResult.Fail(message, errorCode: "SERVICE_NOT_INSTALLED");
            }

            var result = await ExecuteWinSwCommandAsync(serviceId, command, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                var message = BuildFailureMessage($"{actionName}服务失败", result);
                if (string.Equals(command, "start", StringComparison.OrdinalIgnoreCase))
                {
                    message = AppendStartFailureDiagnostics(serviceId, message, result);
                }

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

    private async Task<WinSwCommandResult> ExecuteWinSwCommandAsync(
        string serviceId,
        string command,
        CancellationToken cancellationToken,
        Action<string>? onOutputLine = null)
    {
        var configPath = _paths.GetServiceConfigPath(serviceId);
        var winSwExecutable = _paths.ResolveWinSwExecutablePath();
        var args = $"{command} \"{configPath}\"";
        var result = await _cliExecutor.ExecuteAsync(winSwExecutable, args, cancellationToken, onOutputLine)
            .ConfigureAwait(false);
        if (result.ProcessStartFailed)
        {
            result.StandardError = $"启动 WinSW 失败：{winSwExecutable}"
                                   + Environment.NewLine
                                   + result.StandardError;
        }

        return result;
    }

    private void DeployServiceFiles(ManagedService service)
    {
        var serviceDirectory = _paths.GetServiceDirectory(service.Id);
        Directory.CreateDirectory(serviceDirectory);
        Directory.CreateDirectory(_paths.GetServiceLogsDirectory(service.Id));

        WriteServiceConfigFile(service);
    }

    private void WriteServiceConfigFile(ManagedService service)
    {
        var xmlBytes = _configGenerator.GenerateUtf8Bytes(service);
        File.WriteAllBytes(_paths.GetServiceConfigPath(service.Id), xmlBytes);
    }

    private static bool IsRefreshCommandUnsupported(WinSwCommandResult result)
    {
        var output = (result.StandardOutput + " " + result.StandardError).ToLowerInvariant();
        return output.Contains("refreshcommand")
               || output.Contains("refresh command")
               || output.Contains("unknown command")
               || output.Contains("unrecognized command")
               || output.Contains("is not recognized");
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

    private string AppendStartFailureDiagnostics(string serviceId, string message, WinSwCommandResult result)
    {
        var output = (result.StandardOutput + Environment.NewLine + result.StandardError).Trim();
        var hasAttachConsoleWarning = output.IndexOf("Failed to attach to console", StringComparison.OrdinalIgnoreCase) >= 0;
        var hasChildProcessExit = output.IndexOf("Child process", StringComparison.OrdinalIgnoreCase) >= 0
                                  && output.IndexOf("finished with code", StringComparison.OrdinalIgnoreCase) >= 0;

        var serviceDirectory = _paths.GetServiceDirectory(serviceId);
        var wrapperLog = Path.Combine(serviceDirectory, serviceId + ".wrapper.log");
        var outLog = Path.Combine(serviceDirectory, serviceId + ".out.log");
        var errLog = Path.Combine(serviceDirectory, serviceId + ".err.log");

        if (hasAttachConsoleWarning && hasChildProcessExit)
        {
            return message
                   + Environment.NewLine
                   + "诊断提示：检测到 WinSW 控制台附加告警（Failed to attach to console），这通常是伴随信息；真正根因通常是子进程异常退出。"
                   + Environment.NewLine
                   + $"请优先检查：{errLog}"
                   + Environment.NewLine
                   + $"并对照查看：{wrapperLog}、{outLog}"
                   + Environment.NewLine
                   + "若为 ASP.NET/.NET Worker（net10），请确认程序已按 Windows Service 方式运行（例如 UseWindowsService），并确认服务账号对工作目录/证书/端口有权限。";
        }

        return message
               + Environment.NewLine
               + $"排查日志：{errLog}（应用错误）、{wrapperLog}（WinSW 包装器）、{outLog}（标准输出）。";
    }
}
