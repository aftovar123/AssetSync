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
    internal const string ClientId = "erp-integration";
    internal const string ClientSecret = "s3cret-value";

    private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    internal static TokenService CreateService(DateTime? now = null)
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.UtcNow).Returns(now ?? Now);
        return new TokenService(
            Options.Create(new JwtOptions { SigningKey = SigningKey, TokenLifetimeMinutes = 60 }),
            Options.Create(new ClientCredentialsOptions { ClientId = ClientId, ClientSecret = ClientSecret }),
            clock.Object);
    }

    private static TokenValidationParameters ValidationParameters(DateTime now) => new()
    {
        ValidIssuer = "assetsync-api",
        ValidAudience = "assetsync-clients",
        IssuerSigningKey = TokenService.CreateSigningKey(SigningKey),
        LifetimeValidator = (notBefore, expires, _, _) => notBefore <= now && now < expires,
    };

    [Fact]
    public void AreValidCredentials_CorrectIdAndSecret_ReturnsTrue()
    {
        Assert.True(CreateService().AreValidCredentials(ClientId, ClientSecret));
    }

    [Theory]
    [InlineData(ClientId, "wrong")]
    [InlineData("other-client", ClientSecret)]
    [InlineData(ClientId, "")]
    [InlineData(null, null)]
    public void AreValidCredentials_WrongOrMissing_ReturnsFalse(string? clientId, string? clientSecret)
    {
        Assert.False(CreateService().AreValidCredentials(clientId, clientSecret));
    }

    [Fact]
    public void AreValidCredentials_NoClientConfigured_RejectsEverything()
    {
        var service = new TokenService(
            Options.Create(new JwtOptions { SigningKey = SigningKey }),
            Options.Create(new ClientCredentialsOptions()),
            Mock.Of<IClock>());

        Assert.False(service.AreValidCredentials("", ""));
        Assert.False(service.AreValidCredentials(ClientId, ClientSecret));
    }

    [Fact]
    public async Task Issue_ProducesSignedTokenWithClientAndWriteScope()
    {
        var token = CreateService().Issue(ClientId);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, ValidationParameters(Now.AddMinutes(1)));

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(3600, token.ExpiresInSeconds);
        Assert.Equal(ClientId, result.Claims["client_id"]);
        Assert.Equal(AuthScopes.Write, result.Claims["scope"]);
    }

    [Fact]
    public async Task Issue_TokenIsRejectedAfterItExpires()
    {
        var token = CreateService().Issue(ClientId);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.Value, ValidationParameters(Now.AddMinutes(61)));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Issue_TokenSignedWithAnotherKeyIsRejected()
    {
        var token = CreateService().Issue(ClientId);
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
}
