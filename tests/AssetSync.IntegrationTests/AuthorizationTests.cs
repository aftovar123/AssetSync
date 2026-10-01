using System.Net;
using System.Net.Http.Json;
using static AssetSync.IntegrationTests.AssetSyncApiFactory;

namespace AssetSync.IntegrationTests;

/// <summary>
/// The full permission matrix through the real pipeline (JwtBearer + the
/// per-scope policies), not just the TokenService in isolation. "Allowed"
/// means the request got past authorization — the empty bodies and unknown
/// ids then fail validation or lookup, which is fine and writes nothing.
/// </summary>
[Collection(ApiCollection.Name)]
public class AuthorizationTests(AssetSyncApiFactory factory)
{
    private const string Allowed = "allowed";

    public static TheoryData<string, string, string?, string> Matrix => new()
    {
        { "POST", "/assets", null, "401" },
        { "POST", "/assets", ErpClientId, "403" },
        { "POST", "/assets", AdminClientId, Allowed },
        { "POST", "/work-orders", null, "401" },
        { "POST", "/work-orders", AdminClientId, "403" },
        { "POST", "/work-orders", ErpClientId, Allowed },
        { "POST", "/work-orders/999999/complete", AdminClientId, "403" },
        { "POST", "/work-orders/999999/complete", ErpClientId, Allowed },
        { "GET", "/outbox", null, "401" },
        { "GET", "/outbox", AdminClientId, "403" },
        { "GET", "/outbox", ErpClientId, Allowed },
        { "GET", "/work-orders/1/integration-logs", null, "401" },
        { "GET", "/work-orders/1/integration-logs", AdminClientId, "403" },
        { "GET", "/work-orders/1/integration-logs", ErpClientId, Allowed },
        { "GET", "/assets", null, Allowed },
        { "GET", "/work-orders", null, Allowed },
        { "GET", "/health", null, Allowed },
    };

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Endpoint_EnforcesItsScope(string method, string path, string? clientId, string expected)
    {
        var secret = clientId == ErpClientId ? ErpSecret : AdminSecret;
        var client = await factory.CreateClientAsync(clientId, secret);
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await client.SendAsync(request);

        var status = (int)response.StatusCode;
        if (expected == Allowed)
        {
            Assert.True(status is not (401 or 403), $"{method} {path} as {clientId ?? "anonymous"} returned {status}.");
        }
        else
        {
            Assert.Equal(int.Parse(expected), status);
        }
    }

    [Fact]
    public async Task TokenLimitedToReadScope_CannotWriteWorkOrders()
    {
        var client = await factory.CreateClientAsync(ErpClientId, ErpSecret, scope: "integration.read");

        var write = await client.PostAsJsonAsync("/work-orders", new { });
        var read = await client.GetAsync("/outbox");

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task TamperedToken_IsRejected()
    {
        var client = factory.CreateClient();
        var token = await client.GetTokenAsync(AdminClientId, AdminSecret);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token[..^2] + (token[^2..] == "AA" ? "BB" : "AA"));

        var response = await client.PostAsJsonAsync("/assets", new { code = "X", name = "Y" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
