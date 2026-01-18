using ApiPosture.Core.Models;

namespace ApiPosture.Rules.Exposure;

/// <summary>
/// AP001: Detects public endpoints that don't have explicit [AllowAnonymous].
/// </summary>
public sealed class PublicWithoutExplicitIntentRule : ISecurityRule
{
    public string RuleId => "AP001";
    public string Name => "Public without explicit intent";
    public Severity DefaultSeverity => Severity.High;

    public Finding? Evaluate(Endpoint endpoint)
    {
        // Only triggers if endpoint is public but doesn't have explicit AllowAnonymous
        if (endpoint.Classification != SecurityClassification.Public)
            return null;

        // If AllowAnonymous is explicitly set, the intent is clear
        if (endpoint.Authorization.HasAllowAnonymous)
            return null;

        // Public endpoint without explicit intent
        return new Finding
        {
            RuleId = RuleId,
            RuleName = Name,
            Severity = DefaultSeverity,
            Endpoint = endpoint,
            Message = $"Endpoint '{endpoint.Route}' is publicly accessible but lacks explicit [AllowAnonymous] attribute.",
            Recommendation = "Add [AllowAnonymous] attribute to explicitly document that this endpoint should be public, " +
                           "or add [Authorize] if authentication is required."
        };
    }
}
