namespace OrderService.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string UserName { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    public string MainExchange { get; set; } = "orders.topic";
    public string RetryExchange { get; set; } = "orders.retry.direct";
    public string DeadLetterExchange { get; set; } = "orders.dlx";

    public string OrderCreatedQueue { get; set; } = "orders.v1.order-created";
    public string InventoryEventsQueue { get; set; } = "orders.v1.inventory-events";
    public string RetryQueue { get; set; } = "orders.v1.retry";
    public string DeadLetterQueue { get; set; } = "orders.v1.dlq";

    public int RetryTtlMilliseconds { get; set; } = 5000;
    public int MaxDeliveryAttempts { get; set; } = 3;
}
