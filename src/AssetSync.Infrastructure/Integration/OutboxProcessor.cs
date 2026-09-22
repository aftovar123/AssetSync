using AssetSync.Application.Integration;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssetSync.Infrastructure.Integration;

/// <summary>
/// Thin timer loop — the actual "drain the outbox" logic lives in
/// ProcessOutboxCommandHandler so it stays unit-testable. This class is
/// only the hosting mechanics: run a scope, invoke the handler, wait,
/// repeat, and never let one bad tick kill the loop.
/// </summary>
public class OutboxProcessor(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessor> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var sender = scope.ServiceProvider.GetRequiredService<ISender>();
                var processed = await sender.Send(new ProcessOutboxCommand(), stoppingToken);

                if (processed > 0)
                {
                    logger.LogInformation("Outbox processor handled {Count} pending message(s).", processed);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Outbox processor tick failed; will retry on the next interval.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }
}
