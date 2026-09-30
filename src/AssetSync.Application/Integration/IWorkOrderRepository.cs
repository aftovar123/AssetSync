using AssetSync.Domain;

namespace AssetSync.Application.Integration;

public interface IWorkOrderRepository
{
    Task<WorkOrder?> GetByIdAsync(int id, CancellationToken cancellationToken);

    /// <summary>
    /// Loads the given work orders in a single query and keeps them tracked,
    /// so the <see cref="GetByIdAsync"/> calls that follow in the same scope
    /// are served from memory instead of hitting the database once per id.
    /// </summary>
    Task PreloadAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken);
    Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken);
    Task AddIntegrationLogAsync(IntegrationLog log, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
