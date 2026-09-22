using AssetSync.Application.Integration;
using AssetSync.Domain;
using MediatR;
using Moq;
using Xunit;

namespace AssetSync.Tests.Integration;

public class ProcessOutboxCommandHandlerTests
{
    private readonly Mock<IOutboxRepository> _outboxRepository = new();
    private readonly Mock<ISender> _sender = new();
    private readonly DateTime _fixedNow = new(2026, 9, 22, 12, 30, 0, DateTimeKind.Utc);

    private ProcessOutboxCommandHandler CreateHandler() =>
        new(_outboxRepository.Object, _sender.Object, new FixedClock(_fixedNow));

    [Fact]
    public async Task Handle_SuccessfulSync_MarksMessageProcessed()
    {
        var message = new OutboxMessage { Id = 1, WorkOrderId = 5, CreatedAt = _fixedNow.AddMinutes(-5) };
        _outboxRepository.Setup(o => o.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([message]);
        _sender.Setup(s => s.Send(It.Is<SyncWorkOrderCommand>(c => c.WorkOrderId == 5), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncWorkOrderResult(true, "abc123", null));

        var handler = CreateHandler();
        var processed = await handler.Handle(new ProcessOutboxCommand(), CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal(OutboxMessageStatus.Processed, message.Status);
        Assert.Equal(_fixedNow, message.ProcessedAt);
        _outboxRepository.Verify(o => o.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FailedSync_RecordsFailedAttemptWithoutExhaustingRetries()
    {
        var message = new OutboxMessage { Id = 2, WorkOrderId = 7, CreatedAt = _fixedNow.AddMinutes(-5) };
        _outboxRepository.Setup(o => o.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([message]);
        _sender.Setup(s => s.Send(It.Is<SyncWorkOrderCommand>(c => c.WorkOrderId == 7), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncWorkOrderResult(false, "def456", "ERP unreachable"));

        var handler = CreateHandler();
        var processed = await handler.Handle(new ProcessOutboxCommand(), CancellationToken.None);

        Assert.Equal(1, processed);
        Assert.Equal(1, message.Attempts);
        Assert.Equal("ERP unreachable", message.LastError);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
    }

    [Fact]
    public async Task Handle_NoPendingMessages_DoesNothing()
    {
        _outboxRepository.Setup(o => o.GetPendingAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = CreateHandler();
        var processed = await handler.Handle(new ProcessOutboxCommand(), CancellationToken.None);

        Assert.Equal(0, processed);
        _sender.Verify(s => s.Send(It.IsAny<SyncWorkOrderCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
