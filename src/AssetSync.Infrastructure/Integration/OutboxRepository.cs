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

        // The claim is one atomic statement, so two OutboxProcessor instances
        // running it at the same moment cannot both claim the same row (a
        // SELECT followed by a separate UPDATE could). Rows already
        // Processing but past staleBefore are reclaimed too, in case whoever
        // claimed them crashed mid-batch.
        var claimedIds = db.Database.IsNpgsql()
            // PostgreSQL has no UPDATE ... LIMIT: the subquery locks up to
            // maxBatchSize rows and SKIP LOCKED makes a concurrent claim pass
            // over rows another transaction is taking instead of waiting.
            ? await db.Database.SqlQuery<int>($"""
                UPDATE "OutboxMessages"
                SET "Status" = 'Processing', "ClaimedAt" = {now}
                WHERE "Id" IN (
                    SELECT "Id" FROM "OutboxMessages"
                    WHERE "Status" = 'Pending'
                       OR ("Status" = 'Processing' AND "ClaimedAt" < {staleBefore})
                    ORDER BY "Id"
                    LIMIT {maxBatchSize}
                    FOR UPDATE SKIP LOCKED)
                RETURNING "Id"
                """).ToListAsync(cancellationToken)
            // SQL Server: a single UPDATE TOP ... OUTPUT.
            : await db.Database.SqlQuery<int>($"""
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
