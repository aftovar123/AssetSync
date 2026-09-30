using System.Text;

namespace AssetSync.Api.Auth;

/// <summary>
/// OAuth2 token endpoint, client credentials grant only (RFC 6749 §4.4):
/// machine-to-machine, which is how an ERP integration would call this API.
/// Credentials are accepted in the form body or as HTTP Basic, and errors use
/// the standard OAuth2 shape ({ "error": "..." }) instead of ProblemDetails,
/// because that is what OAuth2 client libraries expect.
/// </summary>
public static class TokenEndpoint
{
    public static IEndpointRouteBuilder MapTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/token", HandleAsync)
            .DisableAntiforgery()
            .AllowAnonymous()
            .WithName("IssueToken");
        return app;
    }

    public static async Task<IResult> HandleAsync(HttpContext context, TokenService tokens)
    {
        if (!context.Request.HasFormContentType)
        {
            return Error("invalid_request", StatusCodes.Status400BadRequest);
        }

        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        if (form["grant_type"] != "client_credentials")
        {
            return Error("unsupported_grant_type", StatusCodes.Status400BadRequest);
        }

        var (clientId, clientSecret) = ReadBasicCredentials(context.Request)
            ?? (form["client_id"].ToString(), form["client_secret"].ToString());

        if (!tokens.AreValidCredentials(clientId, clientSecret))
        {
            context.Response.Headers.WWWAuthenticate = "Basic";
            return Error("invalid_client", StatusCodes.Status401Unauthorized);
        }

        var token = tokens.Issue(clientId);
        // RFC 6749 §5.1: token responses must not be cached.
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new
        {
            access_token = token.Value,
            token_type = "Bearer",
            expires_in = token.ExpiresInSeconds,
            scope = AuthScopes.Write,
        });
    }

    private static (string, string)? ReadBasicCredentials(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
            var separator = decoded.IndexOf(':');
            return separator < 0 ? null : (decoded[..separator], decoded[(separator + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static IResult Error(string error, int statusCode) =>
        Results.Json(new { error }, statusCode: statusCode);
}
