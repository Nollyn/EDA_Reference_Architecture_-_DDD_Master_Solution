using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.Observability;

/// <summary>
/// Enterprise middleware for extracting, generating, and propagating correlation identifiers across HTTP pipelines and OpenTelemetry activities.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    /// <summary>
    /// Default header name for correlation identifier.
    /// </summary>
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CorrelationIdMiddleware"/> class.
    /// </summary>
    /// <param name="next">Next request delegate in the ASP.NET Core pipeline.</param>
    /// <param name="logger">Logger instance.</param>
    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Invokes the middleware to handle correlation identification.
    /// </summary>
    /// <param name="context">Current HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("D");
        }

        var correlationIdStr = correlationId.ToString();
        context.Response.Headers[HeaderName] = correlationIdStr;
        context.Items[HeaderName] = correlationIdStr;

        var currentActivity = Activity.Current;
        currentActivity?.SetTag("correlation.id", correlationIdStr);

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationIdStr }))
        {
            await _next(context);
        }
    }
}
