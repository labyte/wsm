namespace WSM.Core.Models;

/// <summary>
/// 服务运行时状态。
/// </summary>
public enum ServiceRuntimeStatus
{
    NotInstalled,
    Stopped,
    Running,
    StartPending,
    StopPending,
    Error
}
