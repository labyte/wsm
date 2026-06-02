using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WSM.Core.Models;
using WSM.Core.Services;

namespace WSM.App.Shared.ViewModels;

/// <summary>
/// 服务配置草稿模型，用于统一“添加服务”与“修改配置”字段。
/// </summary>
public partial class ServiceConfigDraft : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private ManagedServiceStartMode _startMode = ManagedServiceStartMode.Automatic;

    [ObservableProperty]
    private bool _delayedAutoStart = true;

    [ObservableProperty]
    private bool _autoRefresh = true;

    [ObservableProperty]
    private bool _hideWindow;

    [ObservableProperty]
    private int _stopTimeoutSeconds = 15;

    [ObservableProperty]
    private bool _startAfterInstall = true;

    [ObservableProperty]
    private bool _enableCrashRecovery = true;

    [ObservableProperty]
    private int _crashRestartDelaySeconds = 5;

    [ObservableProperty]
    private FailureActionType _failureAction = FailureActionType.Restart;

    [ObservableProperty]
    private int _resetFailureValue = 1;

    [ObservableProperty]
    private string _resetFailureUnit = "hour";

    [ObservableProperty]
    private string _dependenciesText = string.Empty;

    [ObservableProperty]
    private ServiceLogSourceMode _logSourceMode = ServiceLogSourceMode.WinSw;

    [ObservableProperty]
    private LogMode _logMode = LogMode.RollBySize;

    [ObservableProperty]
    private int _logSizeThresholdKb = 10240;

    [ObservableProperty]
    private int _logKeepFiles = 10;

    [ObservableProperty]
    private string _externalLogFilePath = string.Empty;

    [ObservableProperty]
    private string _externalLogDirectoryPath = string.Empty;

    [ObservableProperty]
    private string _externalLogFileExtensions = ServiceConfigUiOptions.DefaultExternalLogFileExtensions;

    [ObservableProperty]
    private bool _externalLogRealtimeTracking = true;

    [ObservableProperty]
    private int _externalLogTailLines = 500;

    public static ServiceConfigDraft FromManagedService(ManagedService service)
    {
        var (resetValue, resetUnit) = ParseResetFailure(service.FailurePolicy?.ResetFailurePeriod);
        return new ServiceConfigDraft
        {
            Id = service.Id,
            DisplayName = service.DisplayName,
            Description = service.Description,
            ExecutablePath = service.ExecutablePath,
            WorkingDirectory = service.WorkingDirectory,
            Arguments = service.Arguments,
            StartMode = service.StartMode,
            DelayedAutoStart = service.DelayedAutoStart,
            AutoRefresh = service.AutoRefresh,
            HideWindow = service.HideWindow,
            StopTimeoutSeconds = service.StopTimeoutSeconds,
            StartAfterInstall = service.StartAfterInstall,
            EnableCrashRecovery = service.RecoverySettings.EnableCrashRecovery,
            CrashRestartDelaySeconds = GetCrashRestartDelaySeconds(service.FailurePolicy),
            FailureAction = GetFailureAction(service.FailurePolicy),
            ResetFailureValue = resetValue,
            ResetFailureUnit = resetUnit,
            DependenciesText = string.Join(Environment.NewLine, service.Dependencies.Where(x => !string.IsNullOrWhiteSpace(x))),
            LogSourceMode = service.LogSourceMode,
            LogMode = service.LogPolicy.Mode,
            LogSizeThresholdKb = service.LogPolicy.SizeThresholdKb,
            LogKeepFiles = service.LogPolicy.KeepFiles,
            ExternalLogFilePath = service.ExternalLogFilePath,
            ExternalLogDirectoryPath = service.ExternalLogDirectoryPath,
            ExternalLogFileExtensions = string.IsNullOrWhiteSpace(service.ExternalLogFileExtensions)
                ? ServiceConfigUiOptions.DefaultExternalLogFileExtensions
                : service.ExternalLogFileExtensions,
            ExternalLogRealtimeTracking = service.ExternalLogRealtimeTracking,
            ExternalLogTailLines = service.ExternalLogTailLines > 0 ? service.ExternalLogTailLines : 500
        };
    }

    public bool TryValidate(out string error)
    {
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            error = "显示名称不能为空。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ExecutablePath))
        {
            error = "可执行路径不能为空。";
            return false;
        }

        if (!File.Exists(ExecutablePath))
        {
            error = "可执行文件不存在，请检查路径。";
            return false;
        }

        if (StopTimeoutSeconds <= 0)
        {
            error = "停止超时必须大于 0 秒。";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(WorkingDirectory) && !Directory.Exists(WorkingDirectory))
        {
            error = "工作目录不存在，请检查路径。";
            return false;
        }

        if (FailureAction == FailureActionType.Restart && CrashRestartDelaySeconds <= 0)
        {
            error = "重启间隔必须大于 0 秒。";
            return false;
        }

        if (FailureAction == FailureActionType.Restart && ResetFailureValue <= 0)
        {
            error = "失败计数重置值必须大于 0。";
            return false;
        }

        if (LogSourceMode == ServiceLogSourceMode.WinSw && IsWinSwRotationLogMode(LogMode))
        {
            if (LogSizeThresholdKb <= 0)
            {
                error = "日志文件大小上限必须大于 0 KB。";
                return false;
            }

            if (LogKeepFiles <= 0)
            {
                error = "日志保留文件数必须大于 0。";
                return false;
            }
        }
        else if (LogSourceMode == ServiceLogSourceMode.ExternalFile)
        {
            if (string.IsNullOrWhiteSpace(ExternalLogDirectoryPath))
            {
                error = "日志目录不能为空。";
                return false;
            }

            if (!Directory.Exists(ExternalLogDirectoryPath))
            {
                error = "日志目录不存在，请检查路径。";
                return false;
            }

            if (string.IsNullOrWhiteSpace(ExternalLogFileExtensions))
            {
                error = "日志扩展名不能为空。";
                return false;
            }
        }

        var service = new ManagedService
        {
            Id = Id,
            LogPolicy = LogPolicy.CreateDefault()
        };
        ApplyTo(service);

        var validator = new ServiceConfigValidator();
        var result = validator.Validate(service, excludeServiceId: Id);
        if (!result.IsValid)
        {
            error = result.Errors[0].Message;
            return false;
        }

        return true;
    }

    private static bool IsWinSwRotationLogMode(LogMode mode)
        => mode == LogMode.RollBySize
           || mode == LogMode.RollByTime
           || mode == LogMode.RollBySizeTime;

    public void ApplyTo(ManagedService service)
    {
        service.DisplayName = DisplayName.Trim();
        service.Description = Description.Trim();
        service.ExecutablePath = ExecutablePath.Trim();
        service.WorkingDirectory = WorkingDirectory.Trim();
        service.Arguments = Arguments.Trim();
        service.StartMode = StartMode;
        service.DelayedAutoStart = DelayedAutoStart;
        service.AutoRefresh = AutoRefresh;
        service.HideWindow = HideWindow;
        service.StopTimeoutSeconds = StopTimeoutSeconds;
        service.StartAfterInstall = StartAfterInstall;
        service.Dependencies = ParseDependencies(DependenciesText);
        service.LogSourceMode = LogSourceMode;
        service.ExternalLogFilePath = ExternalLogFilePath.Trim();
        service.ExternalLogDirectoryPath = ExternalLogDirectoryPath.Trim();
        service.ExternalLogFileExtensions = NormalizeExtensions(ExternalLogFileExtensions);
        service.ExternalLogRealtimeTracking = ExternalLogRealtimeTracking;
        service.ExternalLogTailLines = Math.Max(1, ExternalLogTailLines);
        service.LogPolicy = BuildLogPolicy(LogSourceMode, LogMode, LogSizeThresholdKb, LogKeepFiles);
        service.FailurePolicy = BuildFailurePolicy(
            FailureAction,
            CrashRestartDelaySeconds,
            ResetFailureValue,
            ResetFailureUnit);
        service.RecoverySettings = new ServiceRecoverySettings
        {
            EnableCrashRecovery = FailureAction == FailureActionType.Restart,
            CrashRestartDelaySeconds = CrashRestartDelaySeconds,
            CrashMaxRestartCount = 1,
            EnableHangRecovery = false,
            HangDetectionTimeoutSeconds = 120,
            EnablePseudoHangRecovery = false,
            PseudoHangTimeoutSeconds = 300,
            RestartOnAnomaly = true
        };
    }

    public static FailurePolicy BuildFailurePolicy(
        FailureActionType action,
        int delaySeconds,
        int resetFailureValue,
        string resetFailureUnit)
    {
        var normalizedAction = action == FailureActionType.None
            ? FailureActionType.None
            : FailureActionType.Restart;

        if (normalizedAction == FailureActionType.None)
        {
            return new FailurePolicy
            {
                ResetFailurePeriod = BuildResetFailurePeriod(resetFailureValue, resetFailureUnit),
                Actions = new List<FailureActionEntry>
                {
                    new FailureActionEntry
                    {
                        Action = FailureActionType.None,
                        Delay = "0 sec"
                    }
                }
            };
        }

        return new FailurePolicy
        {
            ResetFailurePeriod = BuildResetFailurePeriod(resetFailureValue, resetFailureUnit),
            Actions = new List<FailureActionEntry>
            {
                new FailureActionEntry
                {
                    Action = FailureActionType.Restart,
                    Delay = $"{Math.Max(1, delaySeconds)} sec"
                }
            }
        };
    }

    public static LogPolicy BuildLogPolicy(ServiceLogSourceMode sourceMode, LogMode logMode, int sizeThresholdKb, int keepFiles)
    {
        return new LogPolicy
        {
            // 外部日志模式下，WinSW 日志应禁用，避免重复落盘。
            Mode = sourceMode == ServiceLogSourceMode.ExternalFile ? LogMode.Ignore : logMode,
            SizeThresholdKb = Math.Max(1, sizeThresholdKb),
            KeepFiles = Math.Max(1, keepFiles)
        };
    }

    private static int GetCrashRestartDelaySeconds(FailurePolicy? failurePolicy)
    {
        var restartAction = failurePolicy?.Actions?.FirstOrDefault(x => x.Action == FailureActionType.Restart);
        if (restartAction == null || string.IsNullOrWhiteSpace(restartAction.Delay))
        {
            return 5;
        }

        var tokens = restartAction.Delay.Trim()
            .Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return 5;
        }

        return int.TryParse(tokens[0], out var seconds) && seconds > 0 ? seconds : 5;
    }

    private static FailureActionType GetFailureAction(FailurePolicy? failurePolicy)
    {
        var action = failurePolicy?.Actions?.FirstOrDefault();
        if (action == null)
        {
            return FailureActionType.Restart;
        }

        return action.Action == FailureActionType.None
            ? FailureActionType.None
            : FailureActionType.Restart;
    }

    private static (int Value, string Unit) ParseResetFailure(string? resetFailure)
    {
        if (string.IsNullOrWhiteSpace(resetFailure))
        {
            return (1, "hour");
        }

        var normalized = resetFailure ?? string.Empty;
        var tokens = normalized.Trim()
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2 || !int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            return (1, "hour");
        }

        var unitToken = tokens[1].ToLowerInvariant();
        if (unitToken.StartsWith("min", StringComparison.Ordinal))
        {
            return (value, "minute");
        }

        if (unitToken.StartsWith("day", StringComparison.Ordinal))
        {
            return (value, "day");
        }

        if (unitToken.StartsWith("month", StringComparison.Ordinal))
        {
            return (value, "month");
        }

        return (value, "hour");
    }

    private static string BuildResetFailurePeriod(int value, string unit)
    {
        var normalizedValue = Math.Max(1, value);
        var normalizedUnit = (unit ?? "hour").Trim().ToLowerInvariant();
        var singular = normalizedUnit switch
        {
            "minute" => "minute",
            "day" => "day",
            "month" => "month",
            _ => "hour"
        };

        if (normalizedValue == 1)
        {
            return $"{normalizedValue} {singular}";
        }

        return $"{normalizedValue} {singular}s";
    }

    private static List<string> ParseDependencies(string? dependenciesText)
    {
        if (string.IsNullOrWhiteSpace(dependenciesText))
        {
            return new List<string>();
        }

        var normalized = dependenciesText ?? string.Empty;
        return normalized
            .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string NormalizeExtensions(string? extensionsText)
    {
        if (string.IsNullOrWhiteSpace(extensionsText))
        {
            return ServiceConfigUiOptions.DefaultExternalLogFileExtensions;
        }

        var normalizedInput = extensionsText ?? string.Empty;
        var normalized = normalizedInput
            .Split(new[] { ';', ',', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.StartsWith(".") ? x.ToLowerInvariant() : "." + x.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return normalized.Count == 0
            ? ServiceConfigUiOptions.DefaultExternalLogFileExtensions
            : string.Join(";", normalized);
    }
}
