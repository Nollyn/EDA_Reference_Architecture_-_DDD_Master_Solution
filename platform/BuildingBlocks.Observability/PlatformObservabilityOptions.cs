namespace BuildingBlocks.Observability;

/// <summary>
/// Options configuring enterprise OpenTelemetry observability for platform services.
/// </summary>
public sealed class PlatformObservabilityOptions
{
    /// <summary>
    /// Service name identifier for telemetry metrics and distributed traces.
    /// </summary>
    public string ServiceName { get; set; } = "PlatformService";

    /// <summary>
    /// Semantic version of the service.
    /// </summary>
    public string ServiceVersion { get; set; } = "1.0.0";

    /// <summary>
    /// OTLP exporter endpoint (gRPC / HTTP).
    /// </summary>
    public string OtlpEndpoint { get; set; } = "http://localhost:4317";

    /// <summary>
    /// Whether to enable ASP.NET Core instrumentation.
    /// </summary>
    public bool EnableAspNetCoreInstrumentation { get; set; } = true;

    /// <summary>
    /// Whether to enable HttpClient instrumentation.
    /// </summary>
    public bool EnableHttpClientInstrumentation { get; set; } = true;

    /// <summary>
    /// Whether to enable Npgsql PostgreSQL database instrumentation.
    /// </summary>
    public bool EnableNpgsqlInstrumentation { get; set; } = true;

    /// <summary>
    /// Additional custom ActivitySource names to register.
    /// </summary>
    public List<string> AdditionalSources { get; } = [];

    /// <summary>
    /// Additional custom Meter names to register.
    /// </summary>
    public List<string> AdditionalMeters { get; } = [];
}
