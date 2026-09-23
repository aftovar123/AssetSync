using AssetSync.Application.Assets;
using AssetSync.Domain;
using Microsoft.EntityFrameworkCore;

namespace AssetSync.Infrastructure;

public class AssetRepository(AssetSyncDbContext db) : IAssetRepository
{
    public async Task AddAsync(Asset asset, CancellationToken cancellationToken) =>
        await db.Assets.AddAsync(asset, cancellationToken);

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken) =>
        db.Assets.AnyAsync(a => a.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
