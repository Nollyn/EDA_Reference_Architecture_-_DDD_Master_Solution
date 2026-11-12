using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderService.Infrastructure.Messaging.Outbox;

namespace OrderService.Worker.Workers;

public sealed class OutboxBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxBackgroundWorker> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(2);

    public OutboxBackgroundWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Background Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IOutboxProcessor>();

                var processedCount = await processor.ProcessOutboxMessagesAsync(batchSize: 50, stoppingToken);

                // If messages were processed, immediately poll again without waiting
                if (processedCount > 0)
                {
                    continue;
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred while executing Outbox message publishing cycle.");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }

        _logger.LogInformation("Outbox Background Worker stopped.");
    }
}
