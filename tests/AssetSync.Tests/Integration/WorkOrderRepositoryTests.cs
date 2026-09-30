using AssetSync.Domain;
using AssetSync.Infrastructure;
using AssetSync.Infrastructure.Integration;
using Microsoft.EntityFrameworkCore;

namespace AssetSync.Tests.Integration;

public class WorkOrderRepositoryTests
{
    private static DbContextOptions<AssetSyncDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AssetSyncDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static async Task SeedWorkOrders(DbContextOptions<AssetSyncDbContext> options, params int[] ids)
    {
        await using var db = new AssetSyncDbContext(options);
        foreach (var id in ids)
        {
            db.WorkOrders.Add(new WorkOrder { Id = id, AssetId = 1, Description = $"Work order {id}" });
        }
        await db.SaveChangesAsync();
    }

    // The proof that GetByIdAsync does not query again after PreloadAsync:
    // the rows are deleted from the store through another context, and the
    // repository still returns them — they can only come from memory.
    [Fact]
    public async Task GetByIdAsync_AfterPreload_IsServedFromMemoryWithoutQuerying()
    {
        var options = CreateOptions();
        await SeedWorkOrders(options, 5, 7, 9);
        await using var db = new AssetSyncDbContext(options);
        var repository = new WorkOrderRepository(db);

        await repository.PreloadAsync([5, 7], CancellationToken.None);

        await using (var other = new AssetSyncDbContext(options))
        {
            other.WorkOrders.RemoveRange(other.WorkOrders);
            await other.SaveChangesAsync();
        }

        Assert.NotNull(await repository.GetByIdAsync(5, CancellationToken.None));
        Assert.NotNull(await repository.GetByIdAsync(7, CancellationToken.None));
        // Not preloaded, so this one does go to the store — and it is gone.
        Assert.Null(await repository.GetByIdAsync(9, CancellationToken.None));
    }

    [Fact]
    public async Task GetByIdAsync_WithoutPreload_StillLoadsFromTheStore()
    {
        var options = CreateOptions();
        await SeedWorkOrders(options, 5);
        await using var db = new AssetSyncDbContext(options);
        var repository = new WorkOrderRepository(db);

        var workOrder = await repository.GetByIdAsync(5, CancellationToken.None);

        Assert.NotNull(workOrder);
        Assert.Equal("Work order 5", workOrder.Description);
        Assert.Null(await repository.GetByIdAsync(404, CancellationToken.None));
    }
}
