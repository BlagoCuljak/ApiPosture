namespace ApiPosture.Core.Models;

/// <summary>
/// Represents authorization configuration for an endpoint.
/// </summary>
public sealed class AuthorizationInfo
{
    /// <summary>
    /// Whether the endpoint has an [Authorize] attribute (or RequireAuthorization for Minimal APIs).
    /// </summary>
    public bool HasAuthorize { get; init; }

    /// <summary>
    /// Whether the endpoint has an [AllowAnonymous] attribute (or AllowAnonymous for Minimal APIs).
    /// </summary>
    public bool HasAllowAnonymous { get; init; }

    /// <summary>
    /// Roles specified in the authorization attribute.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>
    /// Policies specified in the authorization attribute.
    /// </summary>
    public IReadOnlyList<string> Policies { get; init; } = [];

    /// <summary>
    /// Authentication schemes specified in the authorization attribute.
    /// </summary>
    public IReadOnlyList<string> AuthenticationSchemes { get; init; } = [];

    /// <summary>
    /// Authorization info inherited from controller or route group.
    /// </summary>
    public AuthorizationInfo? InheritedFrom { get; init; }

    /// <summary>
    /// Gets the effective authorization state considering inheritance.
    /// </summary>
    public bool IsEffectivelyAuthorized =>
        HasAuthorize || (InheritedFrom?.IsEffectivelyAuthorized ?? false);

    /// <summary>
    /// Gets whether the endpoint is effectively public (AllowAnonymous overrides Authorize).
    /// </summary>
    public bool IsEffectivelyPublic =>
        HasAllowAnonymous || (!IsEffectivelyAuthorized);

    /// <summary>
    /// Gets all effective roles including inherited ones.
    /// </summary>
    public IReadOnlyList<string> EffectiveRoles =>
        Roles.Concat(InheritedFrom?.EffectiveRoles ?? []).Distinct().ToList();

    /// <summary>
    /// Gets all effective policies including inherited ones.
    /// </summary>
    public IReadOnlyList<string> EffectivePolicies =>
        Policies.Concat(InheritedFrom?.EffectivePolicies ?? []).Distinct().ToList();

    public static AuthorizationInfo Empty => new();
}
