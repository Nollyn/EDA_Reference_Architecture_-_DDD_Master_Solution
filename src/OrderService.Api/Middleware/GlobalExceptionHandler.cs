using System.Diagnostics;
using BuildingBlocks.Observability;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Exceptions;
using OrderService.Domain.Common;

namespace OrderService.Api.Middleware;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred while processing HTTP request: {Path}", httpContext.Request.Path);

        var correlationId = httpContext.Items[CorrelationIdMiddleware.HeaderName]?.ToString()
            ?? httpContext.TraceIdentifier;

        var (statusCode, title, detail, type, extensions) = MapException(exception);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Type = type,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        if (extensions is not null)
        {
            foreach (var (key, value) in extensions)
            {
                problemDetails.Extensions[key] = value;
            }
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private static (int StatusCode, string Title, string Detail, string Type, IDictionary<string, object?>? Extensions) MapException(Exception exception)
    {
        return exception switch
        {
            ValidationException valEx => (
                StatusCodes.Status400BadRequest,
                "Bad Request",
                valEx.Message,
                "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1",
                new Dictionary<string, object?> { ["errors"] = valEx.Errors }),

            DomainException domEx => (
                StatusCodes.Status422UnprocessableEntity,
                "Unprocessable Entity",
                domEx.Message,
                "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.21",
                null),

            NotFoundException notFoundEx => (
                StatusCodes.Status404NotFound,
                "Not Found",
                notFoundEx.Message,
                "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5",
                null),

            ConcurrencyConflictException concEx => (
                StatusCodes.Status409Conflict,
                "Conflict",
                concEx.Message,
                "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10",
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Internal Server Error",
                "An unexpected error occurred. Please contact system administrators.",
                "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1",
                null)
        };
    }
}
