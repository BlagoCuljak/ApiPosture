using ApiPosture.Core.Models;

namespace ApiPosture.Rules.Consistency;

/// <summary>
/// AP003: Detects when [AllowAnonymous] on an action overrides controller-level [Authorize].
/// </summary>
public sealed class ControllerActionConflictRule : ISecurityRule
{
    public string RuleId => "AP003";
    public string Name => "Controller/action authorization conflict";
    public Severity DefaultSeverity => Severity.Medium;

    public Finding? Evaluate(Endpoint endpoint)
    {
        // Only applies to controller endpoints
        if (endpoint.Type != EndpointType.Controller)
            return null;

        // Check if action has AllowAnonymous and controller has Authorize
        if (!endpoint.Authorization.HasAllowAnonymous)
            return null;

        var inherited = endpoint.Authorization.InheritedFrom;
        if (inherited == null || !inherited.HasAuthorize)
            return null;

        return new Finding
        {
            RuleId = RuleId,
            RuleName = Name,
            Severity = DefaultSeverity,
            Endpoint = endpoint,
            Message = $"Endpoint '{endpoint.Route}' has [AllowAnonymous] that overrides controller-level [Authorize].",
            Recommendation = "Verify this override is intentional. Consider moving the action to a separate " +
                           "controller if it has fundamentally different security requirements."
        };
    }
}
