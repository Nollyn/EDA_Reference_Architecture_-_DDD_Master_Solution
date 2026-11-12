using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OrderService.Infrastructure.Messaging.RabbitMq;

public interface IRabbitMqConnectionManager : IAsyncDisposable
{
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
    Task<IChannel> CreateChannelAsync(bool enablePublisherConfirms = false, CancellationToken cancellationToken = default);
}

public sealed class RabbitMqConnectionManager : IRabbitMqConnectionManager
{
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqConnectionManager> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private IConnection? _connection;

    public RabbitMqConnectionManager(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnectionManager> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not null && _connection.IsOpen)
            return _connection;

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not null && _connection.IsOpen)
                return _connection;

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true
            };

            _logger.LogInformation("Connecting to RabbitMQ broker at {Host}:{Port} (VHost: {VHost})...", _options.HostName, _options.Port, _options.VirtualHost);
            _connection = await factory.CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IChannel> CreateChannelAsync(bool enablePublisherConfirms = false, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        CreateChannelOptions? channelOptions = null;

        if (enablePublisherConfirms)
        {
            channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);
        }

        return await connection.CreateChannelAsync(channelOptions, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
        _semaphore.Dispose();
    }
}
