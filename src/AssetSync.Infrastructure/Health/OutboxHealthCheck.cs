using AssetSync.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AssetSync.Infrastructure.Health;

/// <summary>
/// Domain-aware check, not just infrastructure plumbing: a dead-lettered
/// outbox message means the integration is silently stuck on something a
/// database ping alone would never reveal.
/// </summary>
public class OutboxHealthCheck(AssetSyncDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var failedCount = await db.OutboxMessages
            .CountAsync(m => m.Status == OutboxMessageStatus.Failed, cancellationToken);

        return failedCount > 0
            ? HealthCheckResult.Degraded($"{failedCount} aviso(s) al ERP agotaron sus reintentos y requieren revisión manual.")
            : HealthCheckResult.Healthy("Ningún aviso al ERP quedó fallido.");
    }
}
