using AssetSync.Domain;

namespace AssetSync.Application.Integration;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int maxBatchSize, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
