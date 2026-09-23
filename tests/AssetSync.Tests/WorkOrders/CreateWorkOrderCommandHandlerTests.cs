using AssetSync.Application.Integration;
using AssetSync.Application.WorkOrders;
using AssetSync.Domain;
using AssetSync.Tests.Integration;
using Moq;

namespace AssetSync.Tests.WorkOrders;

public class CreateWorkOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_AddsWorkOrderWithCurrentTimeAndSaves()
    {
        var repository = new Mock<IWorkOrderRepository>();
        var fixedNow = new DateTime(2026, 9, 23, 10, 0, 0, DateTimeKind.Utc);
        var handler = new CreateWorkOrderCommandHandler(repository.Object, new FixedClock(fixedNow));

        var result = await handler.Handle(new CreateWorkOrderCommand(1, "Cambio de filtro"), CancellationToken.None);

        Assert.Equal(1, result.AssetId);
        Assert.Equal("Cambio de filtro", result.Description);
        Assert.Equal(fixedNow, result.CreatedAt);
        Assert.Equal(WorkOrderStatus.Open, result.Status);
        repository.Verify(r => r.AddAsync(result, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
