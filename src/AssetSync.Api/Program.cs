using AssetSync.Application.Integration;
using AssetSync.Domain;
using AssetSync.Infrastructure;
using AssetSync.Infrastructure.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AssetSyncDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AssetSyncDb")));
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(AssetSync.Application.AssemblyMarker).Assembly));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<IWorkOrderRepository, WorkOrderRepository>();
builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
builder.Services.AddScoped<SimulatedErpClient>();
builder.Services.AddScoped<IExternalErpClient>(sp => new ResilientErpClient(
    sp.GetRequiredService<SimulatedErpClient>(),
    sp.GetRequiredService<ILogger<ResilientErpClient>>()));
builder.Services.AddScoped<INotificationService, ConsoleNotificationService>();
builder.Services.AddHostedService<OutboxProcessor>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/assets", async (AssetSyncDbContext db) =>
    await db.Assets.ToListAsync())
    .WithName("GetAssets");

app.MapPost("/assets", async (Asset asset, AssetSyncDbContext db) =>
{
    db.Assets.Add(asset);
    await db.SaveChangesAsync();
    return Results.Created($"/assets/{asset.Id}", asset);
})
    .WithName("CreateAsset");

app.MapGet("/work-orders", async (AssetSyncDbContext db) =>
    await db.WorkOrders.ToListAsync())
    .WithName("GetWorkOrders");

app.MapPost("/work-orders", async (WorkOrder workOrder, AssetSyncDbContext db) =>
{
    db.WorkOrders.Add(workOrder);
    await db.SaveChangesAsync();
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
