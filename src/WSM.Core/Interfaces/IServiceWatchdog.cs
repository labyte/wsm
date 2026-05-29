using System;
using WSM.Core.Models;

namespace WSM.Core.Interfaces;

/// <summary>
/// 服务守护监控。
/// </summary>
public interface IServiceWatchdog
{
    void Start();

    void Stop();

    event EventHandler<ServiceAnomalyEventArgs>? AnomalyDetected;
}

/// <summary>
/// 服务异常事件参数。
/// </summary>
public sealed class ServiceAnomalyEventArgs : EventArgs
{
    public string ServiceId { get; set; } = string.Empty;

    public ServiceRuntimeStatus Status { get; set; }

    public string Message { get; set; } = string.Empty;
}
