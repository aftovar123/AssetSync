using AssetSync.Domain;
using MediatR;

namespace AssetSync.Application.Integration;

/// <summary>
/// Mirrors the real integration pattern used in production: a unique
/// submission code as idempotency key, retries on transient failure, an
/// audit log of every attempt, and a notification of the final result —
/// success or failure — either way.
/// </summary>
public class SyncWorkOrderCommandHandler(
    IWorkOrderRepository repository,
    IExternalErpClient erpClient,
    INotificationService notifications,
    IClock clock) : IRequestHandler<SyncWorkOrderCommand, SyncWorkOrderResult>
{
    private const int MaxAttempts = 3;

    public async Task<SyncWorkOrderResult> Handle(SyncWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdAsync(request.WorkOrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Work order {request.WorkOrderId} not found.");

        // Already synced: don't resend — this is the duplicate-prevention check.
        if (workOrder.IsSynced)
        {
            return new SyncWorkOrderResult(true, "already-synced", null);
        }

        // One code for this whole logical attempt, reused across retries, so a
        // receiver that already processed it can recognize a retried request
        // instead of double-applying it.
        var submissionCode = Guid.NewGuid().ToString("N");
        string? lastError = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await erpClient.SubmitWorkOrderAsync(workOrder, submissionCode, cancellationToken);

                workOrder.IsSynced = true;
                await repository.AddIntegrationLogAsync(new IntegrationLog
                {
                    WorkOrderId = workOrder.Id,
                    SubmissionCode = submissionCode,
                    Sent = true,
                    AttemptedAt = clock.UtcNow,
                }, cancellationToken);
                await repository.SaveChangesAsync(cancellationToken);

                await notifications.NotifySuccessAsync(submissionCode, cancellationToken);
                return new SyncWorkOrderResult(true, submissionCode, null);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
            }
        }

        await repository.AddIntegrationLogAsync(new IntegrationLog
        {
            WorkOrderId = workOrder.Id,
            SubmissionCode = submissionCode,
            Sent = false,
            AttemptedAt = clock.UtcNow,
            ErrorMessage = lastError,
        }, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await notifications.NotifyFailureAsync(submissionCode, lastError ?? "Unknown error", cancellationToken);
        return new SyncWorkOrderResult(false, submissionCode, lastError);
    }
}
