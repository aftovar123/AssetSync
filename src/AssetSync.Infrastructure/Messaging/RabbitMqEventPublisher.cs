using System.Text.Json;
using AssetSync.Application.Integration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace AssetSync.Infrastructure.Messaging;

/// <summary>
/// Best-effort: the ERP sync itself is the critical path, already guaranteed
/// by the outbox. Losing a downstream notification is lower-stakes than
/// losing the actual sync, so a broker hiccup here is logged, not thrown —
/// it must never make SyncWorkOrderCommandHandler report a false failure.
/// Keeps one long-lived connection/channel for the app's lifetime instead
/// of reconnecting per publish.
/// </summary>
public sealed class RabbitMqEventPublisher(
    IConfiguration configuration,
    ILogger<RabbitMqEventPublisher> logger) : IEventPublisher, IAsyncDisposable
{
    public const string QueueName = "workorder-synced";

    private readonly string? _connectionString = configuration["RabbitMq:ConnectionString"];
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(WorkOrderSyncedEvent @event, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            logger.LogWarning("RabbitMq:ConnectionString is not configured; skipping WorkOrderSyncedEvent publish.");
            return;
        }

        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            var body = JsonSerializer.SerializeToUtf8Bytes(@event);
            await channel.BasicPublishAsync(exchange: "", routingKey: QueueName, body: body, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to publish WorkOrderSyncedEvent for work order {WorkOrderId}.", @event.WorkOrderId);
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            var factory = new ConnectionFactory { Uri = new Uri(_connectionString!) };
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await _channel.QueueDeclareAsync(
                QueueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
            return _channel;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.DisposeAsync();
        }
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
