using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Infrastructure.Messaging.Inbox;

namespace OrderService.Worker.Workers;

public sealed class InboxConsumerBackgroundWorker : BackgroundService
{
    private readonly IRabbitMqInboxConsumer _inboxConsumer;
    private readonly ILogger<InboxConsumerBackgroundWorker> _logger;

    public InboxConsumerBackgroundWorker(
        IRabbitMqInboxConsumer inboxConsumer,
        ILogger<InboxConsumerBackgroundWorker> logger)
    {
        _inboxConsumer = inboxConsumer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Inbox Consumer Background Worker initializing...");

        try
        {
            await _inboxConsumer.StartConsumingAsync(stoppingToken);
            _logger.LogInformation("Inbox Consumer Background Worker is listening for incoming integration events.");

            // Keep worker alive while cancellation is not requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Inbox Consumer Background Worker is shutting down gracefully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in Inbox Consumer Background Worker.");
            throw;
        }
    }
}
