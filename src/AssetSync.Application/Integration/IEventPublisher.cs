namespace AssetSync.Application.Integration;

public interface IEventPublisher
{
    Task PublishAsync(WorkOrderSyncedEvent @event, CancellationToken cancellationToken);
}
