using ApiPosture.Core.Models;

namespace ApiPosture.Rules.Surface;

/// <summary>
/// AP008: Detects Minimal API endpoints without any authorization chain.
/// </summary>
public sealed class MinimalApiWithoutAuthRule : ISecurityRule
{
    public string RuleId => "AP008";
    public string Name => "Minimal API without authorization";
    public Severity DefaultSeverity => Severity.High;

    public Finding? Evaluate(Endpoint endpoint)
    {
        // Only applies to Minimal API endpoints
        if (endpoint.Type != EndpointType.MinimalApi)
            return null;

        // If explicitly authorized or explicitly anonymous, it's handled
        if (endpoint.Authorization.IsEffectivelyAuthorized ||
            endpoint.Authorization.HasAllowAnonymous)
            return null;

        // GET-only endpoints are lower risk when unauthenticated (read-only, often intentionally public)
        var severity = endpoint.Methods == HttpMethod.Get ? Severity.Medium : DefaultSeverity;

        return new Finding
        {
            RuleId = RuleId,
            RuleName = Name,
            Severity = severity,
            Endpoint = endpoint,
            Message = $"Minimal API endpoint '{endpoint.Route}' has no authorization chain.",
            Recommendation = "Add .RequireAuthorization() to require authentication, or .AllowAnonymous() " +
                           "to explicitly document that public access is intentional."
        };
    }
}
