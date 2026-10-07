using System.Net;
using System.Net.Http.Json;
using static AssetSync.IntegrationTests.AssetSyncApiFactory;

namespace AssetSync.IntegrationTests;

[Collection(ApiCollection.Name)]
public class PaginationAndValidationTests(AssetSyncApiFactory factory)
{
    [Fact]
    public async Task Assets_AreServedInStableNonOverlappingPages()
    {
        var admin = await factory.CreateClientAsync(AdminClientId, AdminSecret);
        for (var i = 0; i < 12; i++)
        {
            var created = await admin.PostAsJsonAsync("/assets", new { code = ApiTestHelpers.UniqueCode("PAGE"), name = $"Asset {i}" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var anonymous = factory.CreateClient();
        var page1 = await (await anonymous.GetAsync("/assets?page=1&pageSize=5")).ReadJsonAsync();
        var page2 = await (await anonymous.GetAsync("/assets?page=2&pageSize=5")).ReadJsonAsync();

        var ids1 = page1.GetProperty("items").EnumerateArray().Select(a => a.GetProperty("id").GetInt32()).ToList();
        var ids2 = page2.GetProperty("items").EnumerateArray().Select(a => a.GetProperty("id").GetInt32()).ToList();
        var total = page1.GetProperty("totalCount").GetInt32();

        Assert.Equal(5, ids1.Count);
        Assert.Equal(5, ids2.Count);
        Assert.Empty(ids1.Intersect(ids2));
        Assert.True(ids1.Max() < ids2.Min(), "Pages must follow the stable ORDER BY Id.");
        Assert.True(total >= 12);
        Assert.Equal((int)Math.Ceiling(total / 5.0), page1.GetProperty("totalPages").GetInt32());
    }

    [Fact]
    public async Task OversizedPageSize_IsCappedAt100()
    {
        var response = await (await factory.CreateClient().GetAsync("/assets?pageSize=5000")).ReadJsonAsync();

        Assert.Equal(100, response.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task InvalidAsset_Returns400WithFieldErrors()
    {
        var admin = await factory.CreateClientAsync(AdminClientId, AdminSecret);

        var response = await admin.PostAsJsonAsync("/assets", new { code = "", name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.ReadJsonAsync()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("Code", out _));
        Assert.True(errors.TryGetProperty("Name", out _));
    }

    [Fact]
    public async Task MistypedJsonField_Returns400NotA500()
    {
        var erp = await factory.CreateClientAsync(ErpClientId, ErpSecret);

        // An asset code where the numeric id goes: the client's mistake.
        var response = await erp.PostAsJsonAsync("/work-orders", new { assetId = "BOMBA-001", description = "Bad type" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task WorkOrders_ReadBackFromTheDatabase_HaveTextStatusAndUtcTimestamps()
    {
        var admin = await factory.CreateClientAsync(AdminClientId, AdminSecret);
        var asset = await (await admin.PostAsJsonAsync("/assets", new { code = ApiTestHelpers.UniqueCode("FMT"), name = "Format test" })).ReadJsonAsync();
        var erp = await factory.CreateClientAsync(ErpClientId, ErpSecret);
        var created = await (await erp.PostAsJsonAsync("/work-orders", new { assetId = asset.GetProperty("id").GetInt32(), description = "Format test" })).ReadJsonAsync();
        var id = created.GetProperty("id").GetInt32();

        var listed = (await (await factory.CreateClient().GetAsync("/work-orders?pageSize=100")).ReadJsonAsync())
            .GetProperty("items").EnumerateArray()
            .Single(w => w.GetProperty("id").GetInt32() == id);

        Assert.Equal("Open", listed.GetProperty("status").GetString());
        Assert.EndsWith("Z", created.GetProperty("createdAt").GetString());
        Assert.EndsWith("Z", listed.GetProperty("createdAt").GetString());
    }

    [Fact]
    public async Task Health_ReportsTheRealDatabaseAsHealthy()
    {
        var health = await (await factory.CreateClient().GetAsync("/health")).ReadJsonAsync();

        var database = health.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("name").GetString() == "database");
        Assert.Equal("Healthy", database.GetProperty("status").GetString());
    }
}
