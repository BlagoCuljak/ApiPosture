using ApiPosture.Core.Analysis;
using ApiPosture.Core.Models;

namespace ApiPosture.Rules.Exposure;

/// <summary>
/// AP001: Detects public endpoints that don't have explicit [AllowAnonymous].
/// </summary>
public sealed class PublicWithoutExplicitIntentRule : ISecurityRule
{
    public string RuleId => "AP001";
    public string Name => "Public without explicit intent";
    public Severity DefaultSeverity => Severity.Low;

    private readonly IAuthorizationAttributeAnalyzer _attributeAnalyzer;

    public PublicWithoutExplicitIntentRule()
    {
        _attributeAnalyzer = new AuthorizationAttributeAnalyzer();
    }

    public Finding? Evaluate(Endpoint endpoint)
    {
        // Only triggers if endpoint is public but doesn't have explicit AllowAnonymous
        if (endpoint.Classification != SecurityClassification.Public)
            return null;

        // If AllowAnonymous is explicitly set, the intent is clear
        if (endpoint.Authorization.HasAllowAnonymous)
            return null;

        // Check for custom authorization attributes
        if (HasCustomAuthorizationAttribute(endpoint))
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

    /// <summary>
    /// Checks if the endpoint has custom authorization attributes by analyzing source code.
    /// Uses semantic analysis to determine if custom attributes implement authorization interfaces.
    /// </summary>
    private bool HasCustomAuthorizationAttribute(Endpoint endpoint)
    {
        // Known non-authorization attributes to skip
        var knownNonAuthAttributes = new[]
        {
            "Route", "Http", "FromBody", "FromQuery", "FromRoute", "FromHeader", "FromForm", "FromServices",
            "Produces", "Consumes", "SwaggerOperation", "Tags", "ApiExplorerSettings",
            "ProducesResponseType", "ApiController", "NonAction", "ActionName",
            "ValidateAntiForgeryToken", "IgnoreAntiforgeryToken"
        };

        try
        {
            // Extract all attributes from the source code
            var attributes = AttributeExtractor.GetEndpointAttributes(endpoint);

            foreach (var attribute in attributes)
            {
                // Skip known non-auth attributes
                if (knownNonAuthAttributes.Any(na =>
                    attribute.Equals(na, StringComparison.OrdinalIgnoreCase)))
                    continue;

                // Skip built-in auth attributes (already handled by Authorization property)
                if (attribute.Equals("Authorize", StringComparison.OrdinalIgnoreCase) ||
                    attribute.Equals("AllowAnonymous", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Analyze with semantic analyzer
                if (_attributeAnalyzer.IsAuthorizationAttribute(attribute, endpoint.Location.FilePath))
                    return true;
            }

            return false;
        }
        catch
        {
            // If analysis fails, assume no custom auth (fail-safe)
            return false;
        }
    }
}
