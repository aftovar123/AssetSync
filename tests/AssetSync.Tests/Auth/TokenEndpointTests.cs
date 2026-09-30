using System.Text;
using System.Text.Json;
using AssetSync.Api.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace AssetSync.Tests.Auth;

public class TokenEndpointTests
{
    private static DefaultHttpContext CreateContext(Dictionary<string, StringValues>? form, out MemoryStream body)
    {
        body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
            Response = { Body = body },
        };
        if (form is not null)
        {
            context.Request.ContentType = "application/x-www-form-urlencoded";
            context.Request.Form = new FormCollection(form);
        }
        return context;
    }

    private static async Task<JsonElement> Execute(DefaultHttpContext context, MemoryStream body)
    {
        var result = await TokenEndpoint.HandleAsync(context, TokenServiceTests.CreateService());
        await result.ExecuteAsync(context);
        body.Position = 0;
        return await JsonSerializer.DeserializeAsync<JsonElement>(body);
    }

    [Fact]
    public async Task HandleAsync_ValidCredentialsInBody_ReturnsBearerToken()
    {
        var context = CreateContext(new()
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = TokenServiceTests.ClientId,
            ["client_secret"] = TokenServiceTests.ClientSecret,
        }, out var body);

        var json = await Execute(context, body);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("Bearer", json.GetProperty("token_type").GetString());
        Assert.False(string.IsNullOrEmpty(json.GetProperty("access_token").GetString()));
        Assert.Equal(3600, json.GetProperty("expires_in").GetInt32());
        Assert.Equal("no-store", context.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public async Task HandleAsync_ValidCredentialsAsBasicAuth_ReturnsBearerToken()
    {
        var context = CreateContext(new() { ["grant_type"] = "client_credentials" }, out var body);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{TokenServiceTests.ClientId}:{TokenServiceTests.ClientSecret}"));
        context.Request.Headers.Authorization = $"Basic {basic}";

        var json = await Execute(context, body);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("Bearer", json.GetProperty("token_type").GetString());
    }

    [Fact]
    public async Task HandleAsync_WrongSecret_Returns401InvalidClient()
    {
        var context = CreateContext(new()
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = TokenServiceTests.ClientId,
            ["client_secret"] = "wrong",
        }, out var body);

        var json = await Execute(context, body);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("invalid_client", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task HandleAsync_OtherGrantType_Returns400UnsupportedGrantType()
    {
        var context = CreateContext(new() { ["grant_type"] = "password" }, out var body);

        var json = await Execute(context, body);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("unsupported_grant_type", json.GetProperty("error").GetString());
    }

    [Fact]
    public async Task HandleAsync_NotAForm_Returns400InvalidRequest()
    {
        var context = CreateContext(form: null, out var body);

        var json = await Execute(context, body);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("invalid_request", json.GetProperty("error").GetString());
    }
}
