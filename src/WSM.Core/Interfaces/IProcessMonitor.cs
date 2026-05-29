using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WSM.Core.Models;

namespace WSM.Core.Interfaces;

/// <summary>
/// 进程指标监控（Modern 版可选）。
/// </summary>
public interface IProcessMonitor
{
    Task<ProcessMetrics?> SampleAsync(string serviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessMetrics>> SampleAllAsync(CancellationToken cancellationToken = default);
}
