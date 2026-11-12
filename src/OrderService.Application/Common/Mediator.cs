using Microsoft.Extensions.DependencyInjection;

namespace OrderService.Application.Common;

public sealed class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));

        var handler = _serviceProvider.GetService(handlerType)
            ?? throw new InvalidOperationException($"No handler registered for request of type '{requestType.FullName}'.");

        var behaviors = _serviceProvider.GetServices(typeof(IPipelineBehavior<,>).MakeGenericType(requestType, typeof(TResponse)))
            .Cast<dynamic>()
            .Reverse()
            .ToList();

        Func<Task<TResponse>> handlerDelegate = () =>
        {
            var method = handlerType.GetMethod("HandleAsync")
                ?? throw new InvalidOperationException($"HandleAsync method not found on handler '{handlerType.FullName}'.");
            return (Task<TResponse>)method.Invoke(handler, [request, cancellationToken])!;
        };

        foreach (var behavior in behaviors)
        {
            var next = handlerDelegate;
            var currentBehavior = behavior;
            handlerDelegate = () => currentBehavior.HandleAsync((dynamic)request, next, cancellationToken);
        }

        return await handlerDelegate();
    }
}
