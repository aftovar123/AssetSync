using AssetSync.Domain;
using AssetSync.Infrastructure;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddDbContext<AssetSyncDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AssetSyncDb")));
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(AssetSync.Application.AssemblyMarker).Assembly));

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

app.Run();
