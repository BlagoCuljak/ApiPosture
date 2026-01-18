using ApiPosture.Core.Models;

namespace ApiPosture.Core.Classification;

/// <summary>
/// Classifies endpoints based on their authorization configuration.
/// </summary>
public sealed class SecurityClassifier
{
    /// <summary>
    /// Classifies an endpoint based on its authorization info.
    /// </summary>
    public SecurityClassification Classify(AuthorizationInfo auth)
    {
        // AllowAnonymous takes precedence
        if (auth.HasAllowAnonymous)
            return SecurityClassification.Public;

        // If no authorization at all (including inherited)
        if (!auth.IsEffectivelyAuthorized)
            return SecurityClassification.Public;

        // Check for policy restrictions
        if (auth.EffectivePolicies.Count > 0)
            return SecurityClassification.PolicyRestricted;

        // Check for role restrictions
        if (auth.EffectiveRoles.Count > 0)
            return SecurityClassification.RoleRestricted;

        // Has Authorize but no specific roles or policies
        return SecurityClassification.Authenticated;
    }
}
