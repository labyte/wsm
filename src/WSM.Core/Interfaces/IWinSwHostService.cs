using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WSM.Core.Models;

namespace WSM.Core.Interfaces;

/// <summary>
/// WinSW 服务宿主操作。
/// </summary>
public interface IWinSwHostService
{
    Task<OperationResult> InstallAsync(ManagedService service, CancellationToken cancellationToken = default);

    Task<OperationResult> UninstallAsync(string serviceId, CancellationToken cancellationToken = default);

    Task<ServiceRuntimeStatus> GetStatusAsync(string serviceId, CancellationToken cancellationToken = default);

    Task<ServiceRuntimeInfo> GetRuntimeInfoAsync(string serviceId, CancellationToken cancellationToken = default);

    Task<OperationResult> StartAsync(string serviceId, CancellationToken cancellationToken = default);

    Task<OperationResult> StopAsync(string serviceId, CancellationToken cancellationToken = default);

    Task<OperationResult> RestartAsync(string serviceId, CancellationToken cancellationToken = default);

    Task<OperationResult> RefreshAsync(ManagedService service, CancellationToken cancellationToken = default);
}
