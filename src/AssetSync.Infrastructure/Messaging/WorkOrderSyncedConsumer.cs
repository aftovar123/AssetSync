using System.Text;
using System.Text.Json;
using AssetSync.Application.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AssetSync.Infrastructure.Messaging;

/// <summary>
/// Proves the event round-trips end to end instead of just trusting that
/// publishing succeeded: a real subscriber, in the same process for this
/// project, but architecturally identical to a separate downstream service.
/// </summary>
public class WorkOrderSyncedConsumer(
    IConfiguration configuration,
    ILogger<WorkOrderSyncedConsumer> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = configuration["RabbitMq:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            logger.LogWarning("RabbitMq:ConnectionString is not configured; WorkOrderSyncedConsumer will not run.");
            return;
        }

        var factory = new ConnectionFactory { Uri = new Uri(connectionString) };
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(
            RabbitMqEventPublisher.QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var json = Encoding.UTF8.GetString(delivery.Body.Span);
            var @event = JsonSerializer.Deserialize<WorkOrderSyncedEvent>(json);
            logger.LogInformation(
                "WorkOrderSyncedEvent received: work order {WorkOrderId}, submission {SubmissionCode}, synced at {SyncedAt}",
                @event?.WorkOrderId, @event?.SubmissionCode, @event?.SyncedAt);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
        };

        await channel.BasicConsumeAsync(
            RabbitMqEventPublisher.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
