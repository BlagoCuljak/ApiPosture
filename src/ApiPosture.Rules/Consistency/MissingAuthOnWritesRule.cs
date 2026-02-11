using ApiPosture.Core.Analysis;
using ApiPosture.Core.Models;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Rules.Consistency;

/// <summary>
/// AP004: Detects public POST/PUT/PATCH/DELETE endpoints without any authorization.
/// </summary>
public sealed class MissingAuthOnWritesRule : ISecurityRule
{
    public string RuleId => "AP004";
    public string Name => "Missing authorization on write operations";
    public Severity DefaultSeverity => Severity.Critical;

    private static readonly HttpMethod WriteMethods =
        HttpMethod.Post | HttpMethod.Put | HttpMethod.Delete | HttpMethod.Patch;

    private readonly IAuthorizationAttributeAnalyzer _attributeAnalyzer;

    public MissingAuthOnWritesRule()
    {
        _attributeAnalyzer = new AuthorizationAttributeAnalyzer();
    }

    public Finding? Evaluate(Endpoint endpoint)
    {
        // Only triggers for public endpoints without explicit AllowAnonymous
        if (endpoint.Classification != SecurityClassification.Public)
            return null;

        // If AllowAnonymous is explicit, rule AP002 handles it
        if (endpoint.Authorization.HasAllowAnonymous)
            return null;

        // Check for custom authorization attributes
        if (HasCustomAuthorizationAttribute(endpoint))
            return null;

        // Check if any write methods are allowed
        if ((endpoint.Methods & WriteMethods) == HttpMethod.None)
            return null;

        var methods = GetWriteMethodsDisplay(endpoint.Methods);
        var (severity, context) = AnonymousWriteClassifier.CategorizeForMissingAuth(endpoint);

        return new Finding
        {
            RuleId = RuleId,
            RuleName = Name,
            Severity = severity,
            Endpoint = endpoint,
            Message = $"Endpoint '{endpoint.Route}' allows {methods} without any authorization. {context}",
            Recommendation = "Add [Authorize] attribute to require authentication, or add [AllowAnonymous] " +
                           "to explicitly document that public write access is intentional."
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

    private static string GetWriteMethodsDisplay(HttpMethod methods)
    {
        var writeMethods = new List<string>();

        if (methods.HasFlag(HttpMethod.Post)) writeMethods.Add("POST");
        if (methods.HasFlag(HttpMethod.Put)) writeMethods.Add("PUT");
        if (methods.HasFlag(HttpMethod.Delete)) writeMethods.Add("DELETE");
        if (methods.HasFlag(HttpMethod.Patch)) writeMethods.Add("PATCH");

        return string.Join(", ", writeMethods);
    }
}
