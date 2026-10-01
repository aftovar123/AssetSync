using AssetSync.Application.Integration;
using AssetSync.Domain;
using AssetSync.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AssetSync.IntegrationTests;

/// <summary>
/// The guarantee the outbox design rests on — two processors never claim
/// the same message — checked against real SQL Server, where the
/// UPDATE TOP ... OUTPUT statement actually runs. The in-memory provider
/// used by the unit tests cannot execute it at all.
/// </summary>
[Collection(ApiCollection.Name)]
public class OutboxClaimTests(AssetSyncApiFactory factory)
{
    private async Task<int> CreateWorkOrderAsync(AssetSyncDbContext db)
    {
        var asset = new Asset { Code = ApiTestHelpers.UniqueCode("CLAIM"), Name = "Claim test asset" };
        db.Assets.Add(asset);
        await db.SaveChangesAsync();
        var workOrder = new WorkOrder { AssetId = asset.Id, Description = "Claim test", CreatedAt = DateTime.UtcNow };
        db.WorkOrders.Add(workOrder);
        await db.SaveChangesAsync();
        return workOrder.Id;
    }

    [Fact]
    public async Task TwoProcessorsClaimingAtTheSameTime_NeverGetTheSameMessage()
    {
        var seeded = new List<int>();
        await factory.WithDbAsync(async db =>
        {
            var workOrderId = await CreateWorkOrderAsync(db);
            var messages = Enumerable.Range(0, 40)
                .Select(_ => new OutboxMessage { WorkOrderId = workOrderId, CreatedAt = DateTime.UtcNow })
                .ToList();
            db.OutboxMessages.AddRange(messages);
            await db.SaveChangesAsync();
            seeded.AddRange(messages.Select(m => m.Id));
        });

        // Separate scopes = separate DbContexts = separate connections, as
        // with two App Service instances. Both claim at the same moment.
        async Task<IReadOnlyList<int>> Claim()
        {
            using var scope = factory.Services.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
            var claimed = await repository.ClaimPendingAsync(30, TimeSpan.FromMinutes(2), CancellationToken.None);
            return claimed.Select(m => m.Id).ToList();
        }

        var results = await Task.WhenAll(Claim(), Claim());

        Assert.Empty(results[0].Intersect(results[1]));
        Assert.Subset(results[0].Concat(results[1]).ToHashSet(), seeded.ToHashSet());
    }

    [Fact]
    public async Task MessageStuckInProcessing_IsReclaimedOnlyAfterTheThreshold()
    {
        int stuckId = 0, activeId = 0;
        await factory.WithDbAsync(async db =>
        {
            var workOrderId = await CreateWorkOrderAsync(db);
            var stuck = new OutboxMessage
            {
                WorkOrderId = workOrderId, CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                Status = OutboxMessageStatus.Processing, ClaimedAt = DateTime.UtcNow.AddMinutes(-5),
            };
            var active = new OutboxMessage
            {
                WorkOrderId = workOrderId, CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                Status = OutboxMessageStatus.Processing, ClaimedAt = DateTime.UtcNow,
            };
            db.OutboxMessages.AddRange(stuck, active);
            await db.SaveChangesAsync();
            (stuckId, activeId) = (stuck.Id, active.Id);
        });

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var claimed = (await repository.ClaimPendingAsync(100, TimeSpan.FromMinutes(2), CancellationToken.None))
            .Select(m => m.Id).ToList();

        Assert.Contains(stuckId, claimed);
        Assert.DoesNotContain(activeId, claimed);
    }
}
