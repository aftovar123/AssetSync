using AssetSync.Domain;

namespace AssetSync.Application.Integration;

public interface IWorkOrderRepository
{
    Task<WorkOrder?> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task AddIntegrationLogAsync(IntegrationLog log, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
