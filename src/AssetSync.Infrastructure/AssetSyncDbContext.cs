using AssetSync.Domain;
using Microsoft.EntityFrameworkCore;

namespace AssetSync.Infrastructure;

public class AssetSyncDbContext(DbContextOptions<AssetSyncDbContext> options) : DbContext(options)
{
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<IntegrationLog> IntegrationLogs => Set<IntegrationLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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
