using AssetSync.Application.Integration;
using AssetSync.Domain;
using Microsoft.EntityFrameworkCore;

namespace AssetSync.Infrastructure.Integration;

public class OutboxRepository(AssetSyncDbContext db) : IOutboxRepository
{
    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        await db.OutboxMessages.AddAsync(message, cancellationToken);

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(int maxBatchSize, CancellationToken cancellationToken) =>
        await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.Attempts < OutboxMessage.MaxAttempts)
            .OrderBy(m => m.CreatedAt)
            .Take(maxBatchSize)
            .ToListAsync(cancellationToken);

    public void MarkProcessed(OutboxMessage message, DateTime processedAt) => message.ProcessedAt = processedAt;

    public void MarkFailedAttempt(OutboxMessage message, string error)
    {
        message.Attempts++;
        message.LastError = error;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
