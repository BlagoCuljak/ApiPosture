namespace ApiPosture.Core.Models;

/// <summary>
/// Classification of an endpoint's security posture.
/// </summary>
public enum SecurityClassification
{
    /// <summary>
    /// Endpoint is publicly accessible (no authorization required).
    /// </summary>
    Public,

    /// <summary>
    /// Endpoint requires authentication but no specific roles or policies.
    /// </summary>
    Authenticated,

    /// <summary>
    /// Endpoint requires specific roles.
    /// </summary>
    RoleRestricted,

    /// <summary>
    /// Endpoint requires specific authorization policies.
    /// </summary>
    PolicyRestricted
}
