using System;
using System.Collections.Generic;
using System.IO;

namespace WSM.Core.Models;

/// <summary>
/// 托管服务完整配置模型。
/// </summary>
public sealed class ManagedService
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ExecutablePath { get; set; } = string.Empty;

    public string WorkingDirectory { get; set; } = string.Empty;

    public string Arguments { get; set; } = string.Empty;

    public IList<EnvVariable> EnvironmentVariables { get; set; } = new List<EnvVariable>();

    public ManagedServiceStartMode StartMode { get; set; } = ManagedServiceStartMode.Automatic;

    public bool DelayedAutoStart { get; set; } = true;

    /// <summary>
    /// 是否启用 WinSW 自动刷新配置。
    /// </summary>
    public bool AutoRefresh { get; set; } = true;

    /// <summary>
    /// 是否隐藏被托管程序窗口。
    /// </summary>
    public bool HideWindow { get; set; }

    public IList<string> Dependencies { get; set; } = new List<string>();

    public int StopTimeoutSeconds { get; set; } = 15;

    public bool StartAfterInstall { get; set; } = true;

    public FailurePolicy FailurePolicy { get; set; } = FailurePolicy.CreateStandard();

    public ServiceRecoverySettings RecoverySettings { get; set; } = ServiceRecoverySettings.CreateDefault();

    public LogPolicy LogPolicy { get; set; } = LogPolicy.CreateDefault();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 创建带默认值的实例。
    /// </summary>
    public static ManagedService CreateDefault(string? executablePath = null)
    {
        var service = new ManagedService();

        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            service.ExecutablePath = executablePath!;
            var directory = Path.GetDirectoryName(executablePath);
            service.WorkingDirectory = directory ?? string.Empty;
        }

        return service;
    }
}
