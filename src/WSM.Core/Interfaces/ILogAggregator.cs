using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WSM.Core.Models;

namespace WSM.Core.Interfaces;

/// <summary>
/// 日志聚合与 Tail 服务。
/// </summary>
public interface ILogAggregator
{
    Task<IReadOnlyList<LogEntry>> QueryAsync(LogQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// 实时跟踪单服务日志；通过 onEntry 回调推送新条目。
    /// </summary>
    Task TailServiceAsync(
        string serviceId,
        LogFilter filter,
        Action<LogEntry> onEntry,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 实时跟踪全部服务混合日志。
    /// </summary>
    Task TailAllAsync(
        LogFilter filter,
        Action<LogEntry> onEntry,
        CancellationToken cancellationToken = default);
}
