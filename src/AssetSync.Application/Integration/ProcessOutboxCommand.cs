using AssetSync.Domain;
using MediatR;

namespace AssetSync.Application.Integration;

/// <summary>Result is the number of outbox messages picked up in this batch.</summary>
public record ProcessOutboxCommand : IRequest<int>;

/// <summary>
/// The actual outbox-draining logic, kept independent of the timer loop
/// that calls it (OutboxProcessor, in Infrastructure) so it's a plain,
/// directly testable MediatR handler like any other use case.
/// </summary>
public class ProcessOutboxCommandHandler(
    IOutboxRepository outboxRepository,
    ISender sender,
    IClock clock) : IRequestHandler<ProcessOutboxCommand, int>
{
    private const int BatchSize = 10;
    private static readonly TimeSpan StaleClaimThreshold = TimeSpan.FromMinutes(2);

    public async Task<int> Handle(ProcessOutboxCommand request, CancellationToken cancellationToken)
    {
        var pending = await outboxRepository.ClaimPendingAsync(BatchSize, StaleClaimThreshold, cancellationToken);

        foreach (var message in pending)
        {
            var result = await sender.Send(new SyncWorkOrderCommand(message.WorkOrderId), cancellationToken);

            if (result.Success)
            {
                message.MarkProcessed(clock.UtcNow);
            }
            else
            {
                message.RecordFailedAttempt(result.ErrorMessage ?? "Unknown error");
            }
        }

        await outboxRepository.SaveChangesAsync(cancellationToken);
        return pending.Count;
    }
}
