using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OrderService.Infrastructure.Messaging.RabbitMq;

public interface IRabbitMqTopologyInitializer
{
    Task InitializeTopologyAsync(CancellationToken cancellationToken = default);
}

public sealed class RabbitMqTopologyInitializer : IRabbitMqTopologyInitializer
{
    private readonly IRabbitMqConnectionManager _connectionManager;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqTopologyInitializer> _logger;

    public RabbitMqTopologyInitializer(
        IRabbitMqConnectionManager connectionManager,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqTopologyInitializer> logger)
    {
        _connectionManager = connectionManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InitializeTopologyAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing RabbitMQ topology (Exchanges, Queues, DLX, Retry)...");

        using var channel = await _connectionManager.CreateChannelAsync(enablePublisherConfirms: false, cancellationToken);

        // 1. Dead Letter Exchange & Queue (DLX / DLQ)
        await channel.ExchangeDeclareAsync(_options.DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(_options.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(_options.DeadLetterQueue, _options.DeadLetterExchange, routingKey: _options.DeadLetterQueue, arguments: null, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(_options.DeadLetterQueue, _options.DeadLetterExchange, routingKey: "orders.dlq", arguments: null, cancellationToken: cancellationToken);

        // 2. Retry Exchange & Queue with TTL Dead-Lettering back to Main Exchange
        var retryArgs = new Dictionary<string, object?>
        {
            ["x-message-ttl"] = _options.RetryTtlMilliseconds,
            ["x-dead-letter-exchange"] = _options.MainExchange
        };

        await channel.ExchangeDeclareAsync(_options.RetryExchange, ExchangeType.Direct, durable: true, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(_options.RetryQueue, durable: true, exclusive: false, autoDelete: false, arguments: retryArgs, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(_options.RetryQueue, _options.RetryExchange, routingKey: _options.RetryQueue, arguments: null, cancellationToken: cancellationToken);

        // 3. Main Topic Exchange
        await channel.ExchangeDeclareAsync(_options.MainExchange, ExchangeType.Topic, durable: true, autoDelete: false, arguments: null, cancellationToken: cancellationToken);

        // 4. Main Queues configured with DLX
        var queueArgs = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = _options.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = _options.DeadLetterQueue
        };

        // Order Created Queue
        await channel.QueueDeclareAsync(_options.OrderCreatedQueue, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(_options.OrderCreatedQueue, _options.MainExchange, routingKey: "OrderService.OrderCreated", arguments: null, cancellationToken: cancellationToken);

        // Inventory Events Queue (Saga events: InventoryReserved, InventoryReservationFailed)
        await channel.QueueDeclareAsync(_options.InventoryEventsQueue, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(_options.InventoryEventsQueue, _options.MainExchange, routingKey: "InventoryService.#", arguments: null, cancellationToken: cancellationToken);

        _logger.LogInformation("RabbitMQ topology initialized successfully.");
    }
}
