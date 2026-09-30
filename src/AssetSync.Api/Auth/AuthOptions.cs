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
/// The machine client allowed to request tokens (OAuth2 client credentials),
/// e.g. the ERP-side integration. Same rule as the signing key: the secret
/// only lives in user-secrets / App Service configuration (Auth__ClientSecret).
/// </summary>
public class ClientCredentialsOptions
{
    public const string SectionName = "Auth";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
}

public static class AuthScopes
{
    public const string Write = "assetsync.write";
}

public static class AuthPolicies
{
    public const string WriteAccess = "WriteAccess";
}
