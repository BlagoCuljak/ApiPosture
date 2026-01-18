using ApiPosture.Core.Models;

namespace ApiPosture.Rules.Privilege;

/// <summary>
/// AP005: Detects endpoints with more than 3 roles assigned.
/// </summary>
public sealed class ExcessiveRoleAccessRule : ISecurityRule
{
    public string RuleId => "AP005";
    public string Name => "Excessive role access";
    public Severity DefaultSeverity => Severity.Low;

    private const int MaxRoles = 3;

    public Finding? Evaluate(Endpoint endpoint)
    {
        var effectiveRoles = endpoint.Authorization.EffectiveRoles;

        if (effectiveRoles.Count <= MaxRoles)
            return null;

        return new Finding
        {
            RuleId = RuleId,
            RuleName = Name,
            Severity = DefaultSeverity,
            Endpoint = endpoint,
            Message = $"Endpoint '{endpoint.Route}' has {effectiveRoles.Count} roles assigned: " +
                     $"{string.Join(", ", effectiveRoles)}.",
            Recommendation = "Consider using a policy-based authorization approach instead of multiple roles. " +
                           "This simplifies maintenance and makes access control more explicit."
        };
    }
}
