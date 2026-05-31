namespace WSM.Core.Models;

/// <summary>
/// 服务自动恢复配置（崩溃/卡死/假死）。
/// </summary>
public sealed class ServiceRecoverySettings
{
    /// <summary>
    /// 启用崩溃恢复（进程退出后按策略重启）。
    /// </summary>
    public bool EnableCrashRecovery { get; set; } = true;

    /// <summary>
    /// 崩溃重启延迟秒数。
    /// </summary>
    public int CrashRestartDelaySeconds { get; set; } = 5;

    /// <summary>
    /// 崩溃最大连续重启次数。
    /// </summary>
    public int CrashMaxRestartCount { get; set; } = 1;

    /// <summary>
    /// 启用卡死恢复（无响应超时）。
    /// </summary>
    public bool EnableHangRecovery { get; set; }

    /// <summary>
    /// 卡死判定超时秒数。
    /// </summary>
    public int HangDetectionTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// 启用假死恢复（长时间异常但未退出）。
    /// </summary>
    public bool EnablePseudoHangRecovery { get; set; }

    /// <summary>
    /// 假死判定超时秒数。
    /// </summary>
    public int PseudoHangTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 异常时自动恢复动作：当前实现为重启服务。
    /// </summary>
    public bool RestartOnAnomaly { get; set; } = true;

    public static ServiceRecoverySettings CreateDefault()
    {
        return new ServiceRecoverySettings();
    }
}
