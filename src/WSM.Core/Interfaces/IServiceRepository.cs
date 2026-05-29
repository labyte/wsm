using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WSM.Core.Models;

namespace WSM.Core.Interfaces;

/// <summary>
/// 托管服务持久化仓库。
/// </summary>
public interface IServiceRepository
{
    Task<IReadOnlyList<ManagedService>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ManagedService?> GetByIdAsync(string serviceId, CancellationToken cancellationToken = default);

    Task SaveAsync(ManagedService service, CancellationToken cancellationToken = default);

    Task DeleteAsync(string serviceId, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string serviceId, CancellationToken cancellationToken = default);
}
