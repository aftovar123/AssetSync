using AssetSync.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AssetSync.Infrastructure;

public class AssetSyncDbContext(DbContextOptions<AssetSyncDbContext> options) : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<IntegrationLog> IntegrationLogs => Set<IntegrationLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    // Every timestamp in the app is UTC (IClock.UtcNow), but SQL Server's
    // datetime2 does not store the kind, so values read back came out as
    // Unspecified and were serialized without the trailing "Z". Marking them
    // as UTC on read makes the API return the same format whether the entity
    // was just created or loaded from the database.
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Asset>(e =>
        {
            e.Property(a => a.Code).HasMaxLength(50);
            e.HasIndex(a => a.Code).IsUnique();
            e.Property(a => a.Name).HasMaxLength(200);
            e.Property(a => a.Location).HasMaxLength(200);
        });

        modelBuilder.Entity<WorkOrder>(e =>
        {
            e.Property(w => w.Description).HasMaxLength(500);
            e.HasOne<Asset>().WithMany().HasForeignKey(w => w.AssetId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MaintenanceRecord>(e =>
        {
            e.Property(m => m.Notes).HasMaxLength(1000);
            e.HasOne<WorkOrder>().WithMany().HasForeignKey(m => m.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IntegrationLog>(e =>
        {
            e.Property(l => l.SubmissionCode).HasMaxLength(64);
            e.HasIndex(l => l.SubmissionCode);
            e.Property(l => l.ErrorMessage).HasMaxLength(2000);
            e.HasOne<WorkOrder>().WithMany().HasForeignKey(l => l.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.Property(m => m.LastError).HasMaxLength(2000);
            e.Property(m => m.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(m => m.Status);
            e.HasOne<WorkOrder>().WithMany().HasForeignKey(m => m.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
