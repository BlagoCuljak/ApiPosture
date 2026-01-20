using ApiPosture.Core.Models;

namespace ApiPosture.Core.Authorization;

/// <summary>
/// Represents project-wide authorization configuration, such as FallbackPolicy.
/// </summary>
public sealed class GlobalAuthorizationInfo
{
    /// <summary>
    /// Whether a FallbackPolicy is configured in AddAuthorization.
    /// </summary>
    public bool HasFallbackPolicy { get; init; }

    /// <summary>
    /// Whether the FallbackPolicy requires authenticated users (RequireAuthenticatedUser()).
    /// </summary>
    public bool FallbackRequiresAuthentication { get; init; }

    /// <summary>
    /// Roles required by the FallbackPolicy, if any.
    /// </summary>
    public IReadOnlyList<string> FallbackRoles { get; init; } = [];

    /// <summary>
    /// Claims required by the FallbackPolicy, if any.
    /// </summary>
    public IReadOnlyList<string> FallbackClaims { get; init; } = [];

    /// <summary>
    /// Whether a DefaultPolicy is configured.
    /// </summary>
    public bool HasDefaultPolicy { get; init; }

    /// <summary>
    /// Gets whether the global configuration effectively protects all endpoints by default.
    /// </summary>
    public bool ProtectsAllEndpointsByDefault =>
        HasFallbackPolicy && (FallbackRequiresAuthentication || FallbackRoles.Count > 0 || FallbackClaims.Count > 0);

    /// <summary>
    /// Converts this global authorization info to an AuthorizationInfo for inheritance purposes.
    /// </summary>
    public AuthorizationInfo ToAuthorizationInfo()
    {
        if (!HasFallbackPolicy)
            return AuthorizationInfo.Empty;

        return new AuthorizationInfo
        {
            HasAuthorize = FallbackRequiresAuthentication || FallbackRoles.Count > 0 || FallbackClaims.Count > 0,
            HasAllowAnonymous = false,
            Roles = FallbackRoles.ToList(),
            Policies = [],
            AuthenticationSchemes = []
        };
    }

    /// <summary>
    /// Empty global authorization info (no FallbackPolicy).
    /// </summary>
    public static GlobalAuthorizationInfo Empty => new();
}
