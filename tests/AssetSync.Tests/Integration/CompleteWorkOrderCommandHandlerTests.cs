using AssetSync.Application.Integration;
using AssetSync.Domain;
using Moq;
using Xunit;

namespace AssetSync.Tests.Integration;

public class CompleteWorkOrderCommandHandlerTests
{
    private readonly Mock<IWorkOrderRepository> _workOrderRepository = new();
    private readonly Mock<IOutboxRepository> _outboxRepository = new();
    private readonly DateTime _fixedNow = new(2026, 9, 22, 12, 0, 0, DateTimeKind.Utc);

    private CompleteWorkOrderCommandHandler CreateHandler() =>
        new(_workOrderRepository.Object, _outboxRepository.Object, new FixedClock(_fixedNow));

    [Fact]
    public async Task Handle_MarksCompletedAndEnqueuesOutboxMessage_InOneSaveChanges()
    {
        var workOrder = new WorkOrder { Id = 5, AssetId = 1, Description = "Cambio de aceite", CreatedAt = _fixedNow.AddDays(-1) };
        _workOrderRepository.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>())).ReturnsAsync(workOrder);

        var handler = CreateHandler();
        await handler.Handle(new CompleteWorkOrderCommand(5), CancellationToken.None);

        Assert.Equal(WorkOrderStatus.Completed, workOrder.Status);
        Assert.Equal(_fixedNow, workOrder.CompletedAt);

        _outboxRepository.Verify(o => o.AddAsync(
            It.Is<OutboxMessage>(m => m.WorkOrderId == 5 && m.CreatedAt == _fixedNow),
            It.IsAny<CancellationToken>()), Times.Once);

        // Exactly one SaveChanges call: the status change and the outbox
        // insert are meant to land in the same transaction.
        _workOrderRepository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _outboxRepository.Verify(o => o.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownWorkOrder_Throws()
    {
        _workOrderRepository.Setup(r => r.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((WorkOrder?)null);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CompleteWorkOrderCommand(99), CancellationToken.None));
    }
}
