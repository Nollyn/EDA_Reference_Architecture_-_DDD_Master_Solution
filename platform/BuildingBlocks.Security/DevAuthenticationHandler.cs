using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BuildingBlocks.Security;

/// <summary>
/// Development authentication handler for local testing and Swagger UI without requiring a real Entra ID/OIDC tenant.
/// </summary>
public sealed class DevAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// Scheme name for local development bypass authentication.
    /// </summary>
    public const string SchemeName = "PlatformDevScheme";

    /// <summary>
    /// Initializes a new instance of the <see cref="DevAuthenticationHandler"/> class.
    /// </summary>
    /// <param name="options">Scheme options monitor.</param>
    /// <param name="logger">Logger factory.</param>
    /// <param name="encoder">Url encoder.</param>
    public DevAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <summary>
    /// Authenticates the request by creating a mock development identity.
    /// </summary>
    /// <returns>AuthenticateResult with developer identity claims.</returns>
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "dev-local-user-id"),
            new Claim(ClaimTypes.Name, "Developer Local"),
            new Claim(ClaimTypes.Email, "dev@enterprise.local"),
            new Claim(ClaimTypes.Role, PlatformPolicies.PlatformAdmin),
            new Claim(ClaimTypes.Role, PlatformPolicies.ServiceOperator),
            new Claim(ClaimTypes.Role, PlatformPolicies.Reader),
            new Claim(ClaimTypes.Role, PlatformPolicies.Writer),
            new Claim("scope", "orders.read orders.write platform.admin")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
