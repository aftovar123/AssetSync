using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AssetSync.Application.Integration;
using AssetSync.Infrastructure;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AssetSync.IntegrationTests;

public static class ApiTestHelpers
{
    public static async Task<HttpClient> CreateClientAsync(this AssetSyncApiFactory factory, string? clientId = null, string? secret = null, string? scope = null)
    {
        var client = factory.CreateClient();
        if (clientId is not null)
        {
            var token = await client.GetTokenAsync(clientId, secret!, scope);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return client;
    }

    public static async Task<string> GetTokenAsync(this HttpClient client, string clientId, string secret, string? scope = null)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        };
        if (scope is not null)
        {
            form["scope"] = scope;
        }

        var response = await client.PostAsync("/auth/token", new FormUrlEncodedContent(form));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("access_token").GetString()!;
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    /// <summary>Does what one OutboxProcessor tick does, on demand.</summary>
    public static async Task<int> ProcessOutboxAsync(this AssetSyncApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessOutboxCommand());
    }

    public static async Task WithDbAsync(this AssetSyncApiFactory factory, Func<AssetSyncDbContext, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<AssetSyncDbContext>());
    }

    public static string UniqueCode(string prefix) => $"{prefix}-{Guid.NewGuid():N}"[..20];
}
