using AssetSync.Application.Integration;
using AssetSync.Domain;
using Microsoft.EntityFrameworkCore;

namespace AssetSync.Infrastructure.Integration;

public class WorkOrderRepository(AssetSyncDbContext db) : IWorkOrderRepository
{
    // FindAsync checks the change tracker before querying, which is what
    // makes PreloadAsync worth calling: a work order already loaded in this
    // scope is returned without another round trip.
    public Task<WorkOrder?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.WorkOrders.FindAsync([id], cancellationToken).AsTask();

    public Task PreloadAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken) =>
        db.WorkOrders.Where(w => ids.Contains(w.Id)).LoadAsync(cancellationToken);

    public async Task AddAsync(WorkOrder workOrder, CancellationToken cancellationToken) =>
        await db.WorkOrders.AddAsync(workOrder, cancellationToken);

    public async Task AddIntegrationLogAsync(IntegrationLog log, CancellationToken cancellationToken) =>
        await db.IntegrationLogs.AddAsync(log, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
