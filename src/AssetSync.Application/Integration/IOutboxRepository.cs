using AssetSync.Domain;

namespace AssetSync.Application.Integration;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically claims up to <paramref name="maxBatchSize"/> messages —
    /// Pending ones, plus any stuck in Processing longer than
    /// <paramref name="staleAfter"/> (their claimer likely crashed). Two
    /// OutboxProcessor instances calling this at the same time can never
    /// claim the same message: the claim itself is a single atomic
    /// UPDATE...OUTPUT, not a SELECT followed by a separate UPDATE.
    /// </summary>
    Task<IReadOnlyList<OutboxMessage>> ClaimPendingAsync(int maxBatchSize, TimeSpan staleAfter, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
