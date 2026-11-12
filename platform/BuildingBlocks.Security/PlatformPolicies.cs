namespace BuildingBlocks.Security;

/// <summary>
/// Standard platform authorization policy and role constants.
/// </summary>
public static class PlatformPolicies
{
    /// <summary>
    /// Policy requiring an authenticated user.
    /// </summary>
    public const string Authenticated = "Authenticated";

    /// <summary>
    /// Policy requiring Platform Administrator role.
    /// </summary>
    public const string PlatformAdmin = "PlatformAdmin";

    /// <summary>
    /// Policy requiring Service Operator role.
    /// </summary>
    public const string ServiceOperator = "ServiceOperator";

    /// <summary>
    /// Policy requiring Read permission.
    /// </summary>
    public const string Reader = "Reader";

    /// <summary>
    /// Policy requiring Write permission.
    /// </summary>
    public const string Writer = "Writer";
}
