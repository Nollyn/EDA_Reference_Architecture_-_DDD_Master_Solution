using OrderService.Application;
using OrderService.Infrastructure;
using OrderService.Worker.Workers;

var builder = Host.CreateApplicationBuilder(args);

// Register Hexagonal Application & Infrastructure
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Register Background Services
builder.Services.AddHostedService<OutboxBackgroundWorker>();
builder.Services.AddHostedService<InboxConsumerBackgroundWorker>();

var host = builder.Build();
host.Run();
