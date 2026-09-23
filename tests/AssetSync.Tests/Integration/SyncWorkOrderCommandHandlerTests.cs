using AssetSync.Application.Common.Exceptions;
using AssetSync.Application.Integration;
using AssetSync.Domain;
using Moq;
using Xunit;

namespace AssetSync.Tests.Integration;

/// <summary>A clock that always answers with a fixed instant — same pattern as Questlog's Clock/SystemClock.</summary>
public class FixedClock(DateTime now) : IClock
{
    public DateTime UtcNow => now;
}

public class SyncWorkOrderCommandHandlerTests
{
    private readonly Mock<IWorkOrderRepository> _repository = new();
    private readonly Mock<IExternalErpClient> _erpClient = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly DateTime _fixedNow = new(2026, 9, 22, 10, 0, 0, DateTimeKind.Utc);

    private SyncWorkOrderCommandHandler CreateHandler() =>
        new(_repository.Object, _erpClient.Object, _notifications.Object, new FixedClock(_fixedNow));

    private static WorkOrder MakeWorkOrder(bool isSynced = false) => new()
    {
        Id = 1,
        AssetId = 1,
        Description = "Cambio de filtro",
        CreatedAt = new DateTime(2026, 9, 1),
        IsSynced = isSynced,
    };

    [Fact]
    public async Task Handle_SubmitsSuccessfully_MarksSyncedAndNotifies()
    {
        var workOrder = MakeWorkOrder();
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(workOrder);

        var handler = CreateHandler();
        var result = await handler.Handle(new SyncWorkOrderCommand(1), CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(workOrder.IsSynced);
        _erpClient.Verify(c => c.SubmitWorkOrderAsync(workOrder, result.SubmissionCode, It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.AddIntegrationLogAsync(
            It.Is<IntegrationLog>(l => l.Sent && l.SubmissionCode == result.SubmissionCode && l.AttemptedAt == _fixedNow),
            It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.NotifySuccessAsync(result.SubmissionCode, It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.NotifyFailureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownWorkOrder_ThrowsNotFound()
    {
        _repository.Setup(r => r.GetByIdAsync(404, It.IsAny<CancellationToken>())).ReturnsAsync((WorkOrder?)null);

        var handler = CreateHandler();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            handler.Handle(new SyncWorkOrderCommand(404), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadySynced_DoesNotResubmit()
    {
        var workOrder = MakeWorkOrder(isSynced: true);
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(workOrder);

        var handler = CreateHandler();
        var result = await handler.Handle(new SyncWorkOrderCommand(1), CancellationToken.None);

        Assert.True(result.Success);
        _erpClient.Verify(c => c.SubmitWorkOrderAsync(It.IsAny<WorkOrder>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.AddIntegrationLogAsync(It.IsAny<IntegrationLog>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ErpClientThrows_LeavesUnsyncedLogsFailureAndNotifies()
    {
        // Retry lives in ResilientErpClient now (see ResilientErpClientTests) —
        // from the handler's point of view, the client either succeeds once
        // or fails once, after whatever retrying already happened underneath.
        var workOrder = MakeWorkOrder();
        _repository.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(workOrder);
        _erpClient
            .Setup(c => c.SubmitWorkOrderAsync(workOrder, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ERP unreachable"));

        var handler = CreateHandler();
        var result = await handler.Handle(new SyncWorkOrderCommand(1), CancellationToken.None);

        Assert.False(result.Success);
        Assert.False(workOrder.IsSynced);
        _erpClient.Verify(c => c.SubmitWorkOrderAsync(workOrder, It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _repository.Verify(r => r.AddIntegrationLogAsync(
            It.Is<IntegrationLog>(l => !l.Sent && l.ErrorMessage == "ERP unreachable" && l.AttemptedAt == _fixedNow),
            It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.NotifyFailureAsync(result.SubmissionCode, "ERP unreachable", It.IsAny<CancellationToken>()), Times.Once);
        _notifications.Verify(n => n.NotifySuccessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
