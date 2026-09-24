using AssetSync.Api;
using AssetSync.Application.Assets;
using AssetSync.Application.Common.Behaviors;
using AssetSync.Application.Integration;
using AssetSync.Application.WorkOrders;
using AssetSync.Domain;
using AssetSync.Infrastructure;
using AssetSync.Infrastructure.Health;
using AssetSync.Infrastructure.Integration;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AssetSyncDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AssetSyncDb")));
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(AssetSync.Application.AssemblyMarker).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(AssetSync.Application.AssemblyMarker).Assembly);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<OutboxHealthCheck>("outbox");

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IAssetRepository, AssetRepository>();
builder.Services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<SimulatedErpClient>();
builder.Services.AddScoped<IExternalErpClient>(sp => new ResilientErpClient(
    sp.GetRequiredService<SimulatedErpClient>(),
    sp.GetRequiredService<ILogger<ResilientErpClient>>()));
builder.Services.AddScoped<INotificationService, ConsoleNotificationService>();
builder.Services.AddHostedService<OutboxProcessor>();

var app = builder.Build();

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
});

app.MapGet("/assets", async (AssetSyncDbContext db) =>
    await db.Assets.ToListAsync())
    .WithName("GetAssets");

app.MapPost("/assets", async (CreateAssetCommand command, ISender sender) =>
{
    var asset = await sender.Send(command);
    return Results.Created($"/assets/{asset.Id}", asset);
})
    .WithName("CreateAsset");

app.MapGet("/work-orders", async (AssetSyncDbContext db) =>
    await db.WorkOrders.ToListAsync())
    .WithName("GetWorkOrders");

app.MapPost("/work-orders", async (CreateWorkOrderCommand command, ISender sender) =>
{
    var workOrder = await sender.Send(command);
    return Results.Created($"/work-orders/{workOrder.Id}", workOrder);
})
    .WithName("CreateWorkOrder");

// Marks the work order completed and enqueues its sync intent atomically
// (one SaveChanges, one transaction). A background processor drains the
// outbox and actually talks to the external system — this endpoint never
// makes that call itself, so it returns immediately.
app.MapPost("/work-orders/{id:int}/complete", async (int id, ISender sender) =>
{
    await sender.Send(new CompleteWorkOrderCommand(id));
    return Results.Accepted();
})
    .WithName("CompleteWorkOrder");

app.MapGet("/work-orders/{id:int}/integration-logs", async (int id, AssetSyncDbContext db) =>
    await db.IntegrationLogs.Where(l => l.WorkOrderId == id).OrderByDescending(l => l.AttemptedAt).ToListAsync())
    .WithName("GetWorkOrderIntegrationLogs");

app.MapGet("/outbox", async (AssetSyncDbContext db) =>
    await db.OutboxMessages.OrderByDescending(m => m.CreatedAt).ToListAsync())
    .WithName("GetOutboxMessages");

app.Run();
