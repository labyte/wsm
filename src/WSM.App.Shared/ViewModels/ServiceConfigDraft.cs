using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WSM.Core.Models;

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
    private int _crashMaxRestartCount = 3;

    public static ServiceConfigDraft FromManagedService(ManagedService service)
    {
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
            CrashMaxRestartCount = GetCrashMaxRestartCount(service.FailurePolicy)
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

        if (StopTimeoutSeconds <= 0)
        {
            error = "停止超时必须大于 0 秒。";
            return false;
        }

        if (EnableCrashRecovery && CrashRestartDelaySeconds <= 0)
        {
            error = "崩溃重启延迟必须大于 0 秒。";
            return false;
        }

        if (EnableCrashRecovery && CrashMaxRestartCount <= 0)
        {
            error = "最大重启次数必须大于 0。";
            return false;
        }

        return true;
    }

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
        service.FailurePolicy = BuildFailurePolicy(
            EnableCrashRecovery,
            CrashRestartDelaySeconds,
            CrashMaxRestartCount);
        service.RecoverySettings = new ServiceRecoverySettings
        {
            EnableCrashRecovery = EnableCrashRecovery,
            CrashRestartDelaySeconds = CrashRestartDelaySeconds,
            CrashMaxRestartCount = CrashMaxRestartCount,
            EnableHangRecovery = false,
            HangDetectionTimeoutSeconds = 120,
            EnablePseudoHangRecovery = false,
            PseudoHangTimeoutSeconds = 300,
            RestartOnAnomaly = true
        };
    }

    public static FailurePolicy BuildFailurePolicy(bool enableCrashRecovery, int delaySeconds, int maxRestartCount)
    {
        if (!enableCrashRecovery)
        {
            return FailurePolicy.CreateFromTemplate(FailurePolicyTemplate.MonitorOnly);
        }

        var actions = new System.Collections.Generic.List<FailureActionEntry>();
        for (var i = 0; i < System.Math.Max(1, maxRestartCount); i++)
        {
            actions.Add(new FailureActionEntry
            {
                Action = FailureActionType.Restart,
                Delay = $"{System.Math.Max(1, delaySeconds)} sec"
            });
        }

        actions.Add(new FailureActionEntry { Action = FailureActionType.None, Delay = "0 sec" });
        return new FailurePolicy
        {
            ResetFailurePeriod = "1 hour",
            Actions = actions
        };
    }

    private static int GetCrashRestartDelaySeconds(FailurePolicy failurePolicy)
    {
        var restartAction = failurePolicy.Actions.FirstOrDefault(x => x.Action == FailureActionType.Restart);
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

    private static int GetCrashMaxRestartCount(FailurePolicy failurePolicy)
    {
        var restartCount = failurePolicy.Actions.Count(x => x.Action == FailureActionType.Restart);
        return System.Math.Max(1, restartCount);
    }
}
