using AssetSync.Api;
using AssetSync.Api.Auth;
using AssetSync.Application.Assets;
using AssetSync.Application.Common.Behaviors;
using AssetSync.Application.Integration;
using AssetSync.Application.WorkOrders;
using AssetSync.Domain;
using AssetSync.Infrastructure;
using AssetSync.Infrastructure.Health;
using AssetSync.Infrastructure.Integration;
using AssetSync.Infrastructure.Messaging;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using Serilog;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.AddAssetSyncTelemetry(out var telemetryWarning);

// Declares the Bearer scheme in the OpenAPI document so Scalar shows an
// "Authorize" box and sends the token on the protected endpoints.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Token from POST /auth/token (client_credentials).",
    };
    document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference("Bearer", document)] = [] }];
    return Task.CompletedTask;
}));
// Retries transient database failures (deadlock victims, dropped
// connections, and the Azure SQL serverless database still waking up from
// auto-pause) with exponential backoff, instead of surfacing them as 500s.
// Every write here is a single SaveChanges, so there is no user transaction
// to wrap manually. SQL Server is the default; an unknown provider fails at
// startup.
var databaseProvider = builder.Configuration.GetValue("Database:Provider", DatabaseProvider.SqlServer);
var connectionString = builder.Configuration.GetConnectionString("AssetSyncDb");
builder.Services.AddDbContext<AssetSyncDbContext>(options => _ = databaseProvider switch
{
    DatabaseProvider.SqlServer => options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(
        maxRetryCount: 5,
        maxRetryDelay: TimeSpan.FromSeconds(10),
        errorNumbersToAdd: null)),
    DatabaseProvider.PostgreSql => options.UseNpgsql(connectionString, npgsql => npgsql
        .MigrationsAssembly("AssetSync.Migrations.PostgreSql")
        .EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null)),
    _ => throw new InvalidOperationException($"Unsupported Database:Provider '{databaseProvider}'."),
});
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(AssetSync.Application.AssemblyMarker).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(AssetSync.Application.AssemblyMarker).Assembly);
// FluentValidation picks its built-in messages ("no debería estar vacío")
// from the server's culture, so the same request answered in Spanish on a
// developer's machine and in English on the Linux App Service. Pinning the
// culture makes every environment answer in Spanish, like the panel.
ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("es");

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Statuses go out as "Processed" or "Completed" instead of 2, so a client
// does not need to know the enum's order; numbers are still accepted on input.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<OutboxHealthCheck>("outbox")
    .AddCheck<MessagingHealthCheck>("messaging");

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IAssetRepository, AssetRepository>();
builder.Services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<SimulatedErpClient>();
builder.Services.AddScoped<IExternalErpClient>(sp => new ResilientErpClient(
    sp.GetRequiredService<SimulatedErpClient>(),
    sp.GetRequiredService<ILogger<ResilientErpClient>>()));
builder.Services.AddScoped<INotificationService, ConsoleNotificationService>();
builder.Services.AddSingleton<RabbitMqEventPublisher>();
builder.Services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<RabbitMqEventPublisher>());
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<WorkOrderSyncedConsumer>();

// JWT bearer auth, OAuth2 client credentials style: the API issues its own
// tokens at POST /auth/token and validates them here with the same key.
// Missing or short key -> the app refuses to start rather than running
// with writes unprotected or signed with a guessable default.
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = TokenService.CreateSigningKey(jwt.SigningKey);
TokenService.ValidateClients(builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions());
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = signingKey,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
// One policy per scope, named after it: an endpoint states the scope it
// needs and a valid token without that scope gets 403 instead of 401.
var authorization = builder.Services.AddAuthorizationBuilder();
foreach (var scope in AuthScopes.All)
{
    authorization.AddPolicy(scope, policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(context => AuthScopes.HasScope(context.User, scope)));
}

// Fixed window per client IP, no queueing: once an IP hits the limit within
// the window it gets 429s immediately instead of piling up threads on the
// free-tier App Service plan.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

// The web panel is served from its own origin (Azure Static Web Apps), so
// the browser needs the API to allow it explicitly. Only the configured
// origins are allowed, and only the methods and headers the panel uses; with
// no origins configured (tests, local runs through the Vite proxy) CORS
// stays off.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .WithMethods("GET", "POST")
    .WithHeaders("Authorization", "Content-Type")));

var app = builder.Build();

if (telemetryWarning is not null)
{
    app.Logger.LogWarning(telemetryWarning);
}

// One structured log line per request (method, path, status, elapsed) —
// separate from the per-feature logging already in ResilientErpClient and
// GlobalExceptionHandler, which flow through the same Serilog pipeline
// without any code change since they just use ILogger<T>.
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
// Before the rate limiter, so a browser's preflight check is answered
// without counting against the client's requests.
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Delegates to GlobalExceptionHandler: ValidationException -> 400,
// NotFoundException -> 404, anything else -> 500. See that class for why.
app.UseExceptionHandler();

// 200 when every check is Healthy or Degraded, 503 when any is Unhealthy —
// the default HealthCheckOptions status-code mapping.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
            }),
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    },
}).DisableRateLimiting();

app.MapTokenEndpoint();

// Asset and work order reads stay public so the live demo can be browsed
// without credentials. Writes need the scope of their area, and the outbox
// and integration logs — internal sync state — need integration.read.

// List endpoints are paginated (?page=1&pageSize=20, capped at 100) and
// read-only, so they skip change tracking. See Pagination.
app.MapGet("/assets", async (int? page, int? pageSize, AssetSyncDbContext db, CancellationToken ct) =>
    await db.Assets.OrderBy(a => a.Id).ToPagedResultAsync(page, pageSize, ct))
    .WithName("GetAssets");

app.MapPost("/assets", async (CreateAssetCommand command, ISender sender) =>
{
    var asset = await sender.Send(command);
    return Results.Created($"/assets/{asset.Id}", asset);
})
    .WithName("CreateAsset")
    .RequireAuthorization(AuthScopes.AssetsWrite);

app.MapGet("/work-orders", async (int? page, int? pageSize, AssetSyncDbContext db, CancellationToken ct) =>
    await db.WorkOrders.OrderByDescending(w => w.CreatedAt).ThenByDescending(w => w.Id).ToPagedResultAsync(page, pageSize, ct))
    .WithName("GetWorkOrders");

app.MapPost("/work-orders", async (CreateWorkOrderCommand command, ISender sender) =>
{
    var workOrder = await sender.Send(command);
    return Results.Created($"/work-orders/{workOrder.Id}", workOrder);
})
    .WithName("CreateWorkOrder")
    .RequireAuthorization(AuthScopes.WorkOrdersWrite);

// Marks the work order completed and enqueues its sync intent atomically
// (one SaveChanges, one transaction). A background processor drains the
// outbox and actually talks to the external system — this endpoint never
// makes that call itself, so it returns immediately.
app.MapPost("/work-orders/{id:int}/complete", async (int id, ISender sender) =>
{
    await sender.Send(new CompleteWorkOrderCommand(id));
    return Results.Accepted();
})
    .WithName("CompleteWorkOrder")
    .RequireAuthorization(AuthScopes.WorkOrdersWrite);

app.MapGet("/work-orders/{id:int}/integration-logs", async (int id, int? page, int? pageSize, AssetSyncDbContext db, CancellationToken ct) =>
    await db.IntegrationLogs.Where(l => l.WorkOrderId == id)
        .OrderByDescending(l => l.AttemptedAt).ThenByDescending(l => l.Id)
        .ToPagedResultAsync(page, pageSize, ct))
    .WithName("GetWorkOrderIntegrationLogs")
    .RequireAuthorization(AuthScopes.IntegrationRead);

app.MapGet("/outbox", async (int? page, int? pageSize, AssetSyncDbContext db, CancellationToken ct) =>
    await db.OutboxMessages.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
        .ToPagedResultAsync(page, pageSize, ct))
    .WithName("GetOutboxMessages")
    .RequireAuthorization(AuthScopes.IntegrationRead);

app.Run();

// Lets the integration tests start this exact app through
// WebApplicationFactory<Program> instead of a hand-built copy of it.
public partial class Program;
