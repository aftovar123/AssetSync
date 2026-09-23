using AssetSync.Application.Common.Exceptions;
using AssetSync.Domain;
using MediatR;

namespace AssetSync.Application.Integration;

public record CompleteWorkOrderCommand(int WorkOrderId) : IRequest<Unit>;

/// <summary>
/// Marks a work order completed and enqueues its sync intent in the same
/// SaveChanges call — one database transaction, so the status change and
/// the outbound intent either both land or neither does.
/// </summary>
public class CompleteWorkOrderCommandHandler(
    IWorkOrderRepository workOrderRepository,
    IOutboxRepository outboxRepository,
    IClock clock) : IRequestHandler<CompleteWorkOrderCommand, Unit>
{
    public async Task<Unit> Handle(CompleteWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var workOrder = await workOrderRepository.GetByIdAsync(request.WorkOrderId, cancellationToken)
            ?? throw new NotFoundException($"Work order {request.WorkOrderId} not found.");

        workOrder.Status = WorkOrderStatus.Completed;
        workOrder.CompletedAt = clock.UtcNow;

        await outboxRepository.AddAsync(new OutboxMessage
        {
            WorkOrderId = workOrder.Id,
            CreatedAt = clock.UtcNow,
        }, cancellationToken);

        await workOrderRepository.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
