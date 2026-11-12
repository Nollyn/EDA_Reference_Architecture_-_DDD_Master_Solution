using System.Diagnostics;

namespace OrderService.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("D");
        }

        context.Response.Headers[HeaderName] = correlationId;
        context.Items[HeaderName] = correlationId.ToString();

        var currentActivity = Activity.Current;
        currentActivity?.SetTag("correlation.id", correlationId.ToString());

        await _next(context);
    }
}
