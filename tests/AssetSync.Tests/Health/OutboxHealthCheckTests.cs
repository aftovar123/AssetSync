using AssetSync.Domain;
using AssetSync.Infrastructure;
using AssetSync.Infrastructure.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssetSync.Tests.Health;

public class OutboxHealthCheckTests
{
    private static AssetSyncDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AssetSyncDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task CheckHealthAsync_NoFailedMessages_ReturnsHealthy()
    {
        await using var db = CreateContext();
        db.OutboxMessages.Add(new OutboxMessage { WorkOrderId = 1, CreatedAt = DateTime.UtcNow, Status = OutboxMessageStatus.Processed });
        await db.SaveChangesAsync();
        var check = new OutboxHealthCheck(db);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_HasFailedMessages_ReturnsDegradedWithCount()
    {
        await using var db = CreateContext();
        db.OutboxMessages.AddRange(
            new OutboxMessage { WorkOrderId = 1, CreatedAt = DateTime.UtcNow, Status = OutboxMessageStatus.Failed, Attempts = 5 },
            new OutboxMessage { WorkOrderId = 2, CreatedAt = DateTime.UtcNow, Status = OutboxMessageStatus.Pending });
        await db.SaveChangesAsync();
        var check = new OutboxHealthCheck(db);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("1", result.Description);
    }
}
