using AssetSync.Domain;

namespace AssetSync.Application.Integration;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken);
    Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int maxBatchSize, CancellationToken cancellationToken);
    void MarkProcessed(OutboxMessage message, DateTime processedAt);
    void MarkFailedAttempt(OutboxMessage message, string error);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
