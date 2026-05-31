using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WSM.Core.Models;

namespace WSM.Core.Services;

/// <summary>
/// 托管服务配置校验器。
/// </summary>
public sealed class ServiceConfigValidator
{
    private static readonly Regex ServiceIdPattern = new Regex(
        "^[a-z][a-z0-9-]*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// 校验完整服务配置。
    /// </summary>
    /// <param name="excludeServiceId">编辑模式时排除自身 ID 的重名检测。</param>
    public ValidationResult Validate(
        ManagedService service,
        IReadOnlyCollection<string>? existingServiceIds = null,
        string? excludeServiceId = null)
    {
        var result = new ValidationResult();

        if (service == null)
        {
            result.AddError(nameof(ManagedService), "服务配置不能为空。");
            return result;
        }

        ValidateId(service.Id, existingServiceIds, excludeServiceId, result);
        ValidateDisplayName(service.DisplayName, result);
        ValidateExecutablePath(service.ExecutablePath, result);
        ValidateWorkingDirectory(service.WorkingDirectory, result);
        ValidateStopTimeout(service.StopTimeoutSeconds, result);
        ValidateLogPolicy(service.LogPolicy, result);
        ValidateLogSource(service, result);
        ValidateFailurePolicy(service.FailurePolicy, result);
        ValidateRecoverySettings(service.RecoverySettings, result);
        ValidateEnvironmentVariables(service.EnvironmentVariables, result);

        return result;
    }

    /// <summary>
    /// 仅校验服务 ID 格式。
    /// </summary>
    public bool IsValidIdFormat(string? serviceId)
    {
        return !string.IsNullOrWhiteSpace(serviceId) && ServiceIdPattern.IsMatch(serviceId);
    }

    private static void ValidateId(
        string? id,
        IReadOnlyCollection<string>? existingServiceIds,
        string? excludeServiceId,
        ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            result.AddError(nameof(ManagedService.Id), "服务 ID 不能为空。");
            return;
        }

        if (!ServiceIdPattern.IsMatch(id))
        {
            result.AddError(nameof(ManagedService.Id), "服务 ID 必须以小写字母开头，且仅包含小写字母、数字和连字符。");
        }

        if (existingServiceIds != null)
        {
            foreach (var existingId in existingServiceIds)
            {
                if (!string.IsNullOrWhiteSpace(excludeServiceId)
                    && string.Equals(existingId, excludeServiceId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.Equals(existingId, id, StringComparison.OrdinalIgnoreCase))
                {
                    result.AddError(nameof(ManagedService.Id), "服务 ID 已存在，请使用其他 ID。");
                    break;
                }
            }
        }
    }

    private static void ValidateDisplayName(string? displayName, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            result.AddError(nameof(ManagedService.DisplayName), "显示名称不能为空。");
        }
    }

    private static void ValidateExecutablePath(string? executablePath, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            result.AddError(nameof(ManagedService.ExecutablePath), "可执行文件路径不能为空。");
            return;
        }

        if (!File.Exists(executablePath))
        {
            result.AddError(nameof(ManagedService.ExecutablePath), "可执行文件不存在，请检查路径。");
        }
    }

    private static void ValidateWorkingDirectory(string? workingDirectory, ValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return;
        }

        if (!Directory.Exists(workingDirectory))
        {
            result.AddError(nameof(ManagedService.WorkingDirectory), "工作目录不存在，请检查路径。");
        }
    }

    private static void ValidateStopTimeout(int stopTimeoutSeconds, ValidationResult result)
    {
        if (stopTimeoutSeconds <= 0)
        {
            result.AddError(nameof(ManagedService.StopTimeoutSeconds), "停止超时必须大于 0 秒。");
        }
    }

    private static void ValidateLogPolicy(LogPolicy? policy, ValidationResult result)
    {
        if (policy == null)
        {
            result.AddError(nameof(ManagedService.LogPolicy), "日志策略不能为空。");
            return;
        }

        if (policy.SizeThresholdKb <= 0)
        {
            result.AddError(nameof(LogPolicy.SizeThresholdKb), "日志文件大小上限必须大于 0 KB。");
        }

        if (policy.KeepFiles <= 0)
        {
            result.AddError(nameof(LogPolicy.KeepFiles), "日志保留文件数必须大于 0。");
        }
    }

    private static void ValidateLogSource(ManagedService service, ValidationResult result)
    {
        if (service.LogSourceMode == ServiceLogSourceMode.WinSw)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(service.ExternalLogDirectoryPath))
        {
            result.AddError(nameof(ManagedService.ExternalLogDirectoryPath), "日志目录不能为空。");
        }

        if (string.IsNullOrWhiteSpace(service.ExternalLogFileExtensions))
        {
            result.AddError(nameof(ManagedService.ExternalLogFileExtensions), "日志扩展名不能为空。");
        }

        if (service.ExternalLogTailLines <= 0)
        {
            result.AddError(nameof(ManagedService.ExternalLogTailLines), "日志 Tail 行数必须大于 0。");
        }
    }

    private static void ValidateFailurePolicy(FailurePolicy? policy, ValidationResult result)
    {
        if (policy == null)
        {
            result.AddError(nameof(ManagedService.FailurePolicy), "失败重启策略不能为空。");
            return;
        }

        if (policy.Actions == null || policy.Actions.Count == 0)
        {
            result.AddError(nameof(FailurePolicy.Actions), "至少配置一条失败动作。");
        }
    }

    private static void ValidateRecoverySettings(ServiceRecoverySettings? settings, ValidationResult result)
    {
        if (settings == null)
        {
            result.AddError(nameof(ManagedService.RecoverySettings), "恢复配置不能为空。");
            return;
        }

        if (settings.EnableCrashRecovery)
        {
            if (settings.CrashRestartDelaySeconds <= 0)
            {
                result.AddError(nameof(ServiceRecoverySettings.CrashRestartDelaySeconds), "崩溃重启延迟必须大于 0 秒。");
            }
        }

        if (settings.EnableHangRecovery && settings.HangDetectionTimeoutSeconds <= 0)
        {
            result.AddError(nameof(ServiceRecoverySettings.HangDetectionTimeoutSeconds), "卡死判定超时必须大于 0 秒。");
        }

        if (settings.EnablePseudoHangRecovery && settings.PseudoHangTimeoutSeconds <= 0)
        {
            result.AddError(nameof(ServiceRecoverySettings.PseudoHangTimeoutSeconds), "假死判定超时必须大于 0 秒。");
        }
    }

    private static void ValidateEnvironmentVariables(IEnumerable<EnvVariable>? variables, ValidationResult result)
    {
        if (variables == null)
        {
            return;
        }

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Name))
            {
                result.AddError(nameof(ManagedService.EnvironmentVariables), "环境变量名称不能为空。");
                continue;
            }

            if (!names.Add(variable.Name))
            {
                result.AddError(nameof(ManagedService.EnvironmentVariables), $"环境变量名称重复：{variable.Name}");
            }
        }
    }
}
