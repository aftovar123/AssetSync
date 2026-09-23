using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssetSync.Infrastructure.Health;

public class DatabaseHealthCheck(AssetSyncDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? HealthCheckResult.Healthy("SQL Server reachable.")
            : HealthCheckResult.Unhealthy("Cannot reach SQL Server.");
    }
}
