using AssetSync.Application.Assets;
using AssetSync.Application.Common.Behaviors;
using AssetSync.Application.Integration;
using AssetSync.Application.WorkOrders;
using AssetSync.Domain;
using AssetSync.Infrastructure;
using AssetSync.Infrastructure.Integration;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AssetSyncDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AssetSyncDb")));
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(AssetSync.Application.AssemblyMarker).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});
builder.Services.AddValidatorsFromAssembly(typeof(AssetSync.Application.AssemblyMarker).Assembly);

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

// FluentValidation failures raised by ValidationBehavior land here as a 400
// with per-field messages. Anything else is an unhandled 500 for now — a
// full ProblemDetails error map is a separate, not-yet-scoped improvement.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    if (error is ValidationException validationException)
    {
        var errors = validationException.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new HttpValidationProblemDetails(errors));
        return;
    }

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new ProblemDetails { Title = "An unexpected error occurred." });
}));

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
