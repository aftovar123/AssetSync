using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AssetSync.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AssetSync.Api.Auth;

public record AccessToken(string Value, int ExpiresInSeconds);

/// <summary>
/// Checks client credentials and issues signed JWTs (HMAC-SHA256). The same
/// key is used by the JwtBearer middleware to validate them — see
/// <see cref="CreateSigningKey"/>.
/// </summary>
public class TokenService(
    IOptions<JwtOptions> jwtOptions,
    IOptions<ClientCredentialsOptions> clientOptions,
    IClock clock)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly ClientCredentialsOptions _client = clientOptions.Value;

    // Fixed-time comparison of hashes: no early exit on the first different
    // byte, and both sides have the same length whatever the input was.
    public bool AreValidCredentials(string? clientId, string? clientSecret)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret)
            || string.IsNullOrEmpty(_client.ClientId) || string.IsNullOrEmpty(_client.ClientSecret))
        {
            return false;
        }

        var idMatches = FixedTimeEquals(clientId, _client.ClientId);
        var secretMatches = FixedTimeEquals(clientSecret, _client.ClientSecret);
        return idMatches & secretMatches;
    }

    public AccessToken Issue(string clientId)
    {
        var now = clock.UtcNow;
        var lifetime = TimeSpan.FromMinutes(_jwt.TokenLifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, clientId),
                new Claim("client_id", clientId),
                new Claim("scope", AuthScopes.Write),
            ]),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(lifetime),
            SigningCredentials = new SigningCredentials(
                CreateSigningKey(_jwt.SigningKey), SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, (int)lifetime.TotalSeconds);
    }

    // HS256 needs at least 256 bits of key; a shorter key is a configuration
    // mistake, so it fails at startup instead of issuing weak tokens.
    public static SymmetricSecurityKey CreateSigningKey(string signingKey)
    {
        var bytes = Encoding.UTF8.GetBytes(signingKey);
        if (bytes.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be set and at least 32 bytes long (user-secrets locally, Jwt__SigningKey in Azure).");
        }
        return new SymmetricSecurityKey(bytes);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(a)),
            SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}
