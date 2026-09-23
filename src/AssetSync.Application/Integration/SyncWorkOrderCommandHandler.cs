using AssetSync.Application.Common.Exceptions;
using AssetSync.Domain;
using MediatR;

namespace AssetSync.Application.Integration;

/// <summary>
/// Mirrors the real integration pattern used in production: a unique
/// submission code as idempotency key, an audit log of every attempt, and
/// a notification of the final result — success or failure — either way.
/// Retry-with-backoff is not this handler's job: whatever IExternalErpClient
/// implementation is wired up owns that (see ResilientErpClient), so this
/// handler only has to react to one clean success-or-failure outcome.
/// </summary>
public class SyncWorkOrderCommandHandler(
    IWorkOrderRepository repository,
    IExternalErpClient erpClient,
    INotificationService notifications,
    IClock clock) : IRequestHandler<SyncWorkOrderCommand, SyncWorkOrderResult>
{
    public async Task<SyncWorkOrderResult> Handle(SyncWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await repository.GetByIdAsync(request.WorkOrderId, cancellationToken)
            ?? throw new NotFoundException($"Work order {request.WorkOrderId} not found.");

        // Already synced: don't resend — this is the duplicate-prevention check.
        if (workOrder.IsSynced)
        {
            return new SyncWorkOrderResult(true, "already-synced", null);
        }

        var submissionCode = Guid.NewGuid().ToString("N");

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
            await repository.AddIntegrationLogAsync(new IntegrationLog
            {
                WorkOrderId = workOrder.Id,
                SubmissionCode = submissionCode,
                Sent = false,
                AttemptedAt = clock.UtcNow,
                ErrorMessage = ex.Message,
            }, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);

            await notifications.NotifyFailureAsync(submissionCode, ex.Message, cancellationToken);
            return new SyncWorkOrderResult(false, submissionCode, ex.Message);
        }
    }
}
