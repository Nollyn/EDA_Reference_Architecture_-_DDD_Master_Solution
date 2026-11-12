using System.Diagnostics;
using System.Text;

namespace BuildingBlocks.Observability;

/// <summary>
/// W3C Trace Context propagation utilities for cross-boundary messaging and distributed transactions.
/// </summary>
public static class TraceContextPropagator
{
    /// <summary>
    /// Injects current W3C traceparent and tracestate into message or transport headers.
    /// </summary>
    /// <param name="headers">Target headers dictionary.</param>
    /// <param name="activity">Optional explicit activity; defaults to <see cref="Activity.Current"/>.</param>
    public static void InjectTraceContext(IDictionary<string, object?> headers, Activity? activity = null)
    {
        activity ??= Activity.Current;
        if (activity is null)
        {
            return;
        }

        headers["traceparent"] = activity.Id;
        if (!string.IsNullOrWhiteSpace(activity.TraceStateString))
        {
            headers["tracestate"] = activity.TraceStateString;
        }
    }

    /// <summary>
    /// Extracts W3C trace context from transport headers.
    /// </summary>
    /// <param name="headers">Source headers dictionary.</param>
    /// <returns>Extracted <see cref="ActivityContext"/> or default if absent.</returns>
    public static ActivityContext ExtractTraceContext(IDictionary<string, object?> headers)
    {
        string? traceParent = null;
        string? traceState = null;

        if (headers.TryGetValue("traceparent", out var tpObj))
        {
            traceParent = tpObj switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string s => s,
                _ => tpObj?.ToString()
            };
        }

        if (headers.TryGetValue("tracestate", out var tsObj))
        {
            traceState = tsObj switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                string s => s,
                _ => tsObj?.ToString()
            };
        }

        if (string.IsNullOrWhiteSpace(traceParent))
        {
            return default;
        }

        return ActivityContext.TryParse(traceParent, traceState, out var context) ? context : default;
    }
}
