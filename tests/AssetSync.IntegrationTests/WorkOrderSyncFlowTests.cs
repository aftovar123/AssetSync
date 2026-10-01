using System.Net;
using System.Net.Http.Json;
using AssetSync.Domain;
using Microsoft.EntityFrameworkCore;
using static AssetSync.IntegrationTests.AssetSyncApiFactory;

namespace AssetSync.IntegrationTests;

[Collection(ApiCollection.Name)]
public class WorkOrderSyncFlowTests(AssetSyncApiFactory factory)
{
    [Fact]
    public async Task CompletedWorkOrders_AreSyncedThroughTheOutbox_EndToEnd()
    {
        var admin = await factory.CreateClientAsync(AdminClientId, AdminSecret);
        var erp = await factory.CreateClientAsync(ErpClientId, ErpSecret);

        var assetResponse = await admin.PostAsJsonAsync("/assets", new { code = ApiTestHelpers.UniqueCode("PUMP"), name = "Bomba centrífuga" });
        Assert.Equal(HttpStatusCode.Created, assetResponse.StatusCode);
        var assetId = (await assetResponse.ReadJsonAsync()).GetProperty("id").GetInt32();

        var workOrderIds = new List<int>();
        foreach (var description in new[] { "Cambio de rodamientos", "Revisión de sellos", "Calibración de presión" })
        {
            var created = await erp.PostAsJsonAsync("/work-orders", new { assetId, description });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.ReadJsonAsync()).GetProperty("id").GetInt32();
            workOrderIds.Add(id);

            var completed = await erp.PostAsync($"/work-orders/{id}/complete", null);
            Assert.Equal(HttpStatusCode.Accepted, completed.StatusCode);
        }

        // Completing only enqueues; nothing is synced until the outbox runs.
        await factory.WithDbAsync(async db =>
            Assert.All(await db.WorkOrders.Where(w => workOrderIds.Contains(w.Id)).ToListAsync(), w => Assert.False(w.IsSynced)));

        var processed = await factory.ProcessOutboxAsync();
        Assert.True(processed >= 3);

        await factory.WithDbAsync(async db =>
        {
            var workOrders = await db.WorkOrders.Where(w => workOrderIds.Contains(w.Id)).ToListAsync();
            Assert.All(workOrders, w =>
            {
                Assert.True(w.IsSynced);
                Assert.Equal(WorkOrderStatus.Completed, w.Status);
            });

            var messages = await db.OutboxMessages.Where(m => workOrderIds.Contains(m.WorkOrderId)).ToListAsync();
            Assert.Equal(3, messages.Count);
            Assert.All(messages, m => Assert.Equal(OutboxMessageStatus.Processed, m.Status));
        });

        var logs = await erp.GetAsync($"/work-orders/{workOrderIds[0]}/integration-logs");
        var logItems = (await logs.ReadJsonAsync()).GetProperty("items");
        Assert.Equal(1, logItems.GetArrayLength());
        Assert.True(logItems[0].GetProperty("sent").GetBoolean());
    }

    [Fact]
    public async Task CompletingTheSameWorkOrderTwice_DoesNotSyncItTwice()
    {
        var admin = await factory.CreateClientAsync(AdminClientId, AdminSecret);
        var erp = await factory.CreateClientAsync(ErpClientId, ErpSecret);
        var assetId = (await (await admin.PostAsJsonAsync("/assets", new { code = ApiTestHelpers.UniqueCode("VALVE"), name = "Válvula" })).ReadJsonAsync()).GetProperty("id").GetInt32();
        var id = (await (await erp.PostAsJsonAsync("/work-orders", new { assetId, description = "Ajuste" })).ReadJsonAsync()).GetProperty("id").GetInt32();

        await erp.PostAsync($"/work-orders/{id}/complete", null);
        await factory.ProcessOutboxAsync();
        var second = await erp.PostAsync($"/work-orders/{id}/complete", null);
        await factory.ProcessOutboxAsync();

        var logs = await (await erp.GetAsync($"/work-orders/{id}/integration-logs")).ReadJsonAsync();
        Assert.Equal(1, logs.GetProperty("items").EnumerateArray().Count(l => l.GetProperty("sent").GetBoolean()));
        Assert.NotEqual(HttpStatusCode.InternalServerError, second.StatusCode);
    }
}
