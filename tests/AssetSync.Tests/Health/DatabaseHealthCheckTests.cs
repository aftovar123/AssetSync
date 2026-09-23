using AssetSync.Infrastructure;
using AssetSync.Infrastructure.Health;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssetSync.Tests.Health;

public class DatabaseHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_CanConnect_ReturnsHealthy()
    {
        var options = new DbContextOptionsBuilder<AssetSyncDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AssetSyncDbContext(options);
        var check = new DatabaseHealthCheck(db);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
