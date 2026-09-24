using AssetSync.Application.Integration;
using AssetSync.Domain;
using Microsoft.EntityFrameworkCore;

namespace AssetSync.Infrastructure.Integration;

public class OutboxRepository(AssetSyncDbContext db, IClock clock) : IOutboxRepository
{
    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken) =>
        await db.OutboxMessages.AddAsync(message, cancellationToken);

    public async Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(
        int maxBatchSize, TimeSpan staleAfter, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var staleBefore = now - staleAfter;

        // A single UPDATE...OUTPUT is one atomic statement in SQL Server —
        // unlike a SELECT followed by a separate UPDATE, two OutboxProcessor
        // instances running this at the same moment cannot both claim the
        // same row. Rows already Processing but past staleBefore are
        // reclaimed too, in case whoever claimed them crashed mid-batch.
        var claimedIds = await db.Database.SqlQuery<int>($"""
            UPDATE TOP ({maxBatchSize}) OutboxMessages
            SET Status = 'Processing', ClaimedAt = {now}
            OUTPUT INSERTED.Id
            WHERE Status = 'Pending'
               OR (Status = 'Processing' AND ClaimedAt < {staleBefore})
            """).ToListAsync(cancellationToken);

        if (claimedIds.Count == 0)
        {
            return [];
        }

        return await db.OutboxMessages
            .Where(m => claimedIds.Contains(m.Id))
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
