using System.Security.Claims;

namespace AssetSync.Api.Auth;

/// <summary>
/// Settings for the tokens this API issues and validates. SigningKey has no
/// default on purpose: it comes from user-secrets locally and from the
/// App Service configuration (Jwt__SigningKey) in Azure, never from git.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "assetsync-api";
    public string Audience { get; set; } = "assetsync-clients";
    public string SigningKey { get; set; } = string.Empty;
    public int TokenLifetimeMinutes { get; set; } = 60;
}

/// <summary>
/// The machine clients allowed to request tokens (OAuth2 client credentials),
/// each limited to its own scopes. Secrets only live in user-secrets / App
/// Service configuration (Auth__Clients__0__ClientSecret, ...).
/// </summary>
public class AuthOptions
{
    public const string SectionName = "Auth";

    public List<AuthClient> Clients { get; set; } = [];
}

public class AuthClient
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>Space-separated, as in the OAuth2 "scope" parameter.</summary>
    public string Scopes { get; set; } = string.Empty;

    public IReadOnlyList<string> ScopeList => AuthScopes.Parse(Scopes);
}

/// <summary>
/// One scope per area of the API, so a client only gets what its job needs:
/// the ERP integration can complete work orders but not create assets.
/// Each scope is also the name of the authorization policy that requires it.
/// </summary>
public static class AuthScopes
{
    public const string AssetsWrite = "assets.write";
    public const string WorkOrdersWrite = "workorders.write";
    public const string IntegrationRead = "integration.read";

    public static readonly IReadOnlyList<string> All = [AssetsWrite, WorkOrdersWrite, IntegrationRead];

    public static IReadOnlyList<string> Parse(string? scopes) =>
        (scopes ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // The token carries a single space-separated "scope" claim (RFC 9068),
    // so the check splits it instead of looking for one claim per scope.
    public static bool HasScope(ClaimsPrincipal user, string scope) =>
        user.FindAll("scope").SelectMany(c => Parse(c.Value)).Contains(scope);
}
