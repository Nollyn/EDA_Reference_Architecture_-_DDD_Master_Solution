using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Security;

/// <summary>
/// Extension methods for configuring enterprise platform authentication and authorization.
/// </summary>
public static class SecurityExtensions
{
    /// <summary>
    /// Registers enterprise platform authentication (JWT Bearer / OIDC / Azure Entra ID) and default authorization policies.
    /// In local development/test mode, enables configurable bypass to allow frictionless Swagger UI usage.
    /// </summary>
    /// <param name="services">Target service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="configure">Optional customization callback for <see cref="PlatformSecurityOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPlatformAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<PlatformSecurityOptions>? configure = null)
    {
        var options = new PlatformSecurityOptions();
        configuration.GetSection(PlatformSecurityOptions.SectionName).Bind(options);

        // Fallback checks for common environment variables
        if (bool.TryParse(configuration["Authentication:EnableDevMode"], out var devMode))
        {
            options.EnableDevMode = devMode;
        }

        configure?.Invoke(options);

        services.AddSingleton(options);

        var authBuilder = services.AddAuthentication(auth =>
        {
            if (options.EnableDevMode)
            {
                auth.DefaultAuthenticateScheme = DevAuthenticationHandler.SchemeName;
                auth.DefaultChallengeScheme = DevAuthenticationHandler.SchemeName;
            }
            else
            {
                auth.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                auth.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }
        });

        if (options.EnableDevMode)
        {
            authBuilder.AddScheme<AuthenticationSchemeOptions, DevAuthenticationHandler>(
                DevAuthenticationHandler.SchemeName, _ => { });
        }
        else
        {
            authBuilder.AddJwtBearer(jwt =>
            {
                jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
                if (!string.IsNullOrWhiteSpace(options.Authority))
                {
                    jwt.Authority = options.Authority;
                }

                if (!string.IsNullOrWhiteSpace(options.MetadataAddress))
                {
                    jwt.MetadataAddress = options.MetadataAddress;
                }

                if (!string.IsNullOrWhiteSpace(options.Audience))
                {
                    jwt.Audience = options.Audience;
                }

                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = !string.IsNullOrWhiteSpace(options.Issuer) || !string.IsNullOrWhiteSpace(options.Authority),
                    ValidIssuer = options.Issuer ?? options.Authority,
                    ValidateAudience = !string.IsNullOrWhiteSpace(options.Audience),
                    ValidAudience = options.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true
                };
            });
        }

        services.AddAuthorization(authz =>
        {
            authz.AddPolicy(PlatformPolicies.Authenticated, p => p.RequireAuthenticatedUser());
            authz.AddPolicy(PlatformPolicies.PlatformAdmin, p => p.RequireRole(PlatformPolicies.PlatformAdmin));
            authz.AddPolicy(PlatformPolicies.ServiceOperator, p => p.RequireRole(PlatformPolicies.ServiceOperator, PlatformPolicies.PlatformAdmin));
            authz.AddPolicy(PlatformPolicies.Reader, p => p.RequireRole(PlatformPolicies.Reader, PlatformPolicies.PlatformAdmin));
            authz.AddPolicy(PlatformPolicies.Writer, p => p.RequireRole(PlatformPolicies.Writer, PlatformPolicies.PlatformAdmin));
        });

        return services;
    }

    /// <summary>
    /// Adds authentication and authorization middlewares into the request processing pipeline.
    /// </summary>
    /// <param name="app">Target application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    public static IApplicationBuilder UsePlatformSecurity(this IApplicationBuilder app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
