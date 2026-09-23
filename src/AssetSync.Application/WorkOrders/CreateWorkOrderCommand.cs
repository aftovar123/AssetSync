using AssetSync.Application.Integration;
using AssetSync.Domain;
using MediatR;

namespace AssetSync.Application.WorkOrders;

public record CreateWorkOrderCommand(int AssetId, string Description) : IRequest<WorkOrder>;

public class CreateWorkOrderCommandHandler(IWorkOrderRepository repository, IClock clock)
    : IRequestHandler<CreateWorkOrderCommand, WorkOrder>
{
    public async Task<WorkOrder> Handle(CreateWorkOrderCommand request, CancellationToken cancellationToken)
    {
        var workOrder = new WorkOrder
        {
            AssetId = request.AssetId,
            Description = request.Description,
            CreatedAt = clock.UtcNow,
        };

        await repository.AddAsync(workOrder, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return workOrder;
    }
}
