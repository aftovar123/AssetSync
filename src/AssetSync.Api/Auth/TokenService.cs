using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AssetSync.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace AssetSync.Api.Auth;

public record AccessToken(string Value, int ExpiresInSeconds, IReadOnlyList<string> Scopes);

/// <summary>
/// Checks client credentials and issues signed JWTs (HMAC-SHA256). The same
/// key is used by the JwtBearer middleware to validate them — see
/// <see cref="CreateSigningKey"/>.
/// </summary>
public class TokenService(
    IOptions<JwtOptions> jwtOptions,
    IOptions<AuthOptions> authOptions,
    IClock clock)
{
    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly IReadOnlyList<AuthClient> _clients = authOptions.Value.Clients;

    // Every configured client is compared, with fixed-time comparisons of
    // hashes, so neither the response time nor an early exit reveals which
    // client id exists or how much of a secret matched.
    public AuthClient? FindClient(string? clientId, string? clientSecret)
    {
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return null;
        }

        AuthClient? match = null;
        foreach (var client in _clients)
        {
            if (string.IsNullOrEmpty(client.ClientId) || string.IsNullOrEmpty(client.ClientSecret))
            {
                continue;
            }

            var idMatches = FixedTimeEquals(clientId, client.ClientId);
            var secretMatches = FixedTimeEquals(clientSecret, client.ClientSecret);
            if (idMatches & secretMatches)
            {
                match = client;
            }
        }
        return match;
    }

    /// <summary>
    /// RFC 6749 §3.3: no "scope" parameter means every scope the client is
    /// allowed; otherwise each requested scope must be one of them, or the
    /// whole request is refused (invalid_scope) rather than silently trimmed.
    /// </summary>
    public static bool TryResolveScopes(AuthClient client, string? requested, out IReadOnlyList<string> granted)
    {
        var allowed = client.ScopeList;
        var asked = AuthScopes.Parse(requested);

        if (asked.Count == 0)
        {
            granted = allowed;
            return allowed.Count > 0;
        }

        granted = asked.Distinct().ToArray();
        return granted.All(allowed.Contains);
    }

    public AccessToken Issue(AuthClient client, IReadOnlyList<string> scopes)
    {
        var now = clock.UtcNow;
        var lifetime = TimeSpan.FromMinutes(_jwt.TokenLifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, client.ClientId),
                new Claim("client_id", client.ClientId),
                new Claim("scope", string.Join(' ', scopes)),
            ]),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(lifetime),
            SigningCredentials = new SigningCredentials(
                CreateSigningKey(_jwt.SigningKey), SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, (int)lifetime.TotalSeconds, scopes);
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

    // A typo such as "workorder.write" would otherwise leave a client unable
    // to call anything, with no hint why — so it stops the app at startup.
    public static void ValidateClients(AuthOptions options)
    {
        var unknown = options.Clients
            .SelectMany(c => c.ScopeList.Where(s => !AuthScopes.All.Contains(s)).Select(s => $"{c.ClientId}: {s}"))
            .ToList();
        if (unknown.Count > 0)
        {
            throw new InvalidOperationException(
                $"Unknown scope(s) in Auth:Clients ({string.Join(", ", unknown)}). Valid scopes: {string.Join(", ", AuthScopes.All)}.");
        }
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(a)),
            SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}
