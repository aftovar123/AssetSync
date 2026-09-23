using AssetSync.Domain;

namespace AssetSync.Application.Assets;

public interface IAssetRepository
{
    Task AddAsync(Asset asset, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
