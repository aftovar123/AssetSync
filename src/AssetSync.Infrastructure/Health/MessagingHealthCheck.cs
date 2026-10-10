using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace AssetSync.Infrastructure.Health;

public class MessagingHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Messaging is best-effort (see RabbitMqEventPublisher) — a broken
        // broker degrades observability, it doesn't take the API down, so
        // this reports Degraded rather than Unhealthy (which would flip the
        // whole /health response to 503).
        var connectionString = configuration["RabbitMq:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return HealthCheckResult.Degraded("RabbitMQ no está configurado (falta RabbitMq:ConnectionString).");
        }

        try
        {
            var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
            await using var connection = await factory.CreateConnectionAsync(cancellationToken);
            return HealthCheckResult.Healthy("RabbitMQ disponible.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("No se puede conectar con RabbitMQ.", ex);
        }
    }
}
