namespace BuildingBlocks.Security;

/// <summary>
/// Options configuring enterprise authentication and authorization across platform services.
/// </summary>
public sealed class PlatformSecurityOptions
{
    /// <summary>
    /// Configuration section key name in appsettings.
    /// </summary>
    public const string SectionName = "PlatformSecurity";

    /// <summary>
    /// OIDC / Azure Entra ID authority URL.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>
    /// Expected token audience (e.g. api://orderservice).
    /// </summary>
    public string? Audience { get; set; }

    /// <summary>
    /// Valid token issuer URL.
    /// </summary>
    public string? Issuer { get; set; }

    /// <summary>
    /// OpenID Connect metadata address endpoint.
    /// </summary>
    public string? MetadataAddress { get; set; }

    /// <summary>
    /// Whether HTTPS metadata is strictly required (should be true in production).
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = false;

    /// <summary>
    /// When enabled, provides a mock/bypass identity for local development and test environments without blocking Swagger UI.
    /// </summary>
    public bool EnableDevMode { get; set; } = false;
}
