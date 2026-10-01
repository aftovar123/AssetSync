using System.Security.Claims;
using AssetSync.Api.Auth;
using AssetSync.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace AssetSync.Tests.Auth;

public class TokenServiceTests
{
    internal const string SigningKey = "test-signing-key-that-is-at-least-32-bytes!";
    internal const string ErpClientId = "erp-integration";
    internal const string ErpSecret = "erp-s3cret";
    internal const string AdminClientId = "asset-admin";
    internal const string AdminSecret = "admin-s3cret";

    private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    internal static AuthClient Erp => new()
    {
        ClientId = ErpClientId,
        ClientSecret = ErpSecret,
        Scopes = $"{AuthScopes.WorkOrdersWrite} {AuthScopes.IntegrationRead}",
    };

    internal static AuthClient Admin => new()
    {
        ClientId = AdminClientId,
        ClientSecret = AdminSecret,
        Scopes = AuthScopes.AssetsWrite,
    };

    internal static TokenService CreateService(DateTime? now = null)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(now ?? Now);
        return new TokenService(
            Options.Create(new JwtOptions { SigningKey = SigningKey, TokenLifetimeMinutes = 60 }),
            Options.Create(new AuthOptions { Clients = [Erp, Admin] }),
            clock.Object);
    }

    private static TokenValidationParameters ValidationParameters(DateTime now) => new()
    {
        ValidIssuer = "assetsync-api",
        ValidAudience = "assetsync-clients",
        IssuerSigningKey = TokenService.CreateSigningKey(SigningKey),
        LifetimeValidator = (notBefore, expires, _, _) => notBefore <= now && now < expires,
    };

    [Theory]
    [InlineData(ErpClientId, ErpSecret, ErpClientId)]
    [InlineData(AdminClientId, AdminSecret, AdminClientId)]
    public void FindClient_CorrectCredentials_ReturnsThatClient(string id, string secret, string expected)
    {
        Assert.Equal(expected, CreateService().FindClient(id, secret)?.ClientId);
    }

    [Theory]
    [InlineData(ErpClientId, "wrong")]
    [InlineData(ErpClientId, AdminSecret)] // another client's secret
    [InlineData("other-client", ErpSecret)]
    [InlineData(ErpClientId, "")]
    [InlineData(null, null)]
    public void FindClient_WrongOrMissing_ReturnsNull(string? clientId, string? clientSecret)
    {
        Assert.Null(CreateService().FindClient(clientId, clientSecret));
    }

    [Fact]
    public void FindClient_NoClientsConfigured_RejectsEverything()
    {
        var service = new TokenService(
            Options.Create(new JwtOptions { SigningKey = SigningKey }),
            Options.Create(new AuthOptions()),
            Mock.Of<IClock>());

        Assert.Null(service.FindClient("", ""));
        Assert.Null(service.FindClient(ErpClientId, ErpSecret));
    }

    [Fact]
    public void TryResolveScopes_NoneRequested_GrantsAllAllowedScopes()
    {
        Assert.True(TokenService.TryResolveScopes(Erp, null, out var granted));
        Assert.Equal([AuthScopes.WorkOrdersWrite, AuthScopes.IntegrationRead], granted);
    }

    [Fact]
    public void TryResolveScopes_SubsetRequested_GrantsOnlyThatSubset()
    {
        Assert.True(TokenService.TryResolveScopes(Erp, AuthScopes.IntegrationRead, out var granted));
        Assert.Equal([AuthScopes.IntegrationRead], granted);
    }

    [Theory]
    [InlineData(AuthScopes.AssetsWrite)]
    [InlineData("integration.read assets.write")]
    [InlineData("made.up")]
    public void TryResolveScopes_ScopeNotAllowedForClient_IsRefused(string requested)
    {
        Assert.False(TokenService.TryResolveScopes(Erp, requested, out _));
    }

    [Fact]
    public async Task Issue_ProducesSignedTokenWithClientAndSpaceSeparatedScopes()
    {
        var token = CreateService().Issue(Erp, Erp.ScopeList);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, ValidationParameters(Now.AddMinutes(1)));

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(3600, token.ExpiresInSeconds);
        Assert.Equal(ErpClientId, result.Claims["client_id"]);
        Assert.Equal("workorders.write integration.read", result.Claims["scope"]);
    }

    [Fact]
    public async Task Issue_TokenIsRejectedAfterItExpires()
    {
        var token = CreateService().Issue(Erp, Erp.ScopeList);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, ValidationParameters(Now.AddMinutes(61)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Issue_TokenSignedWithAnotherKeyIsRejected()
    {
        var token = CreateService().Issue(Erp, Erp.ScopeList);
        var parameters = ValidationParameters(Now.AddMinutes(1));
        parameters.IssuerSigningKey = TokenService.CreateSigningKey("a-completely-different-key-of-32-bytes!!");

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, parameters);

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short-key")]
    public void CreateSigningKey_MissingOrShortKey_Throws(string key)
    {
        Assert.Throws<InvalidOperationException>(() => TokenService.CreateSigningKey(key));
    }

    [Fact]
    public void ValidateClients_UnknownScope_ThrowsNamingClientAndScope()
    {
        var options = new AuthOptions { Clients = [new AuthClient { ClientId = "erp", Scopes = "workorder.write" }] };

        var ex = Assert.Throws<InvalidOperationException>(() => TokenService.ValidateClients(options));
        Assert.Contains("erp: workorder.write", ex.Message);
    }

    [Fact]
    public void ValidateClients_KnownScopes_DoesNotThrow()
    {
        TokenService.ValidateClients(new AuthOptions { Clients = [Erp, Admin] });
    }

    [Theory]
    [InlineData("workorders.write integration.read", AuthScopes.IntegrationRead, true)]
    [InlineData("workorders.write integration.read", AuthScopes.AssetsWrite, false)]
    [InlineData("assets.write", AuthScopes.AssetsWrite, true)]
    [InlineData("assets.writer", AuthScopes.AssetsWrite, false)] // no partial matches
    public void HasScope_ChecksEachSpaceSeparatedScope(string claim, string required, bool expected)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("scope", claim)], "Bearer"));

        Assert.Equal(expected, AuthScopes.HasScope(user, required));
    }
}
