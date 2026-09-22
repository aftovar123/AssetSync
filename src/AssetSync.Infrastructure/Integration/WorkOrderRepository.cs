using AssetSync.Application.Integration;
using AssetSync.Domain;
using Microsoft.EntityFrameworkCore;

namespace AssetSync.Infrastructure.Integration;

public class WorkOrderRepository(AssetSyncDbContext db) : IWorkOrderRepository
{
    public Task<WorkOrder?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.WorkOrders.FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task AddIntegrationLogAsync(IntegrationLog log, CancellationToken cancellationToken) =>
        await db.IntegrationLogs.AddAsync(log, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
