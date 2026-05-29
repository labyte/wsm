using System;

namespace WSM.Core.Models;

/// <summary>
/// 服务运行时快照。
/// </summary>
public sealed class ServiceRuntimeInfo
{
    public ServiceRuntimeStatus Status { get; set; } = ServiceRuntimeStatus.NotInstalled;

    /// <summary>
    /// 当前进程启动时间（本地时区）；未运行时为 null。
    /// </summary>
    public DateTime? StartedAt { get; set; }

    public static ServiceRuntimeInfo NotInstalled()
    {
        return new ServiceRuntimeInfo { Status = ServiceRuntimeStatus.NotInstalled };
    }
}
