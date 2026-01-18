using ApiPosture.Core.Models;

namespace ApiPosture.Rules.Surface;

/// <summary>
/// AP007: Detects sensitive keywords in public route paths.
/// </summary>
public sealed class SensitiveRouteKeywordsRule : ISecurityRule
{
    public string RuleId => "AP007";
    public string Name => "Sensitive route keywords exposed";
    public Severity DefaultSeverity => Severity.Medium;

    private static readonly string[] SensitiveKeywords =
    [
        "admin",
        "debug",
        "export",
        "import",
        "internal",
        "secret",
        "private",
        "config",
        "settings",
        "management",
        "system",
        "diagnostic",
        "backup",
        "migrate",
        "reset",
        "delete-all",
        "purge"
    ];

    public Finding? Evaluate(Endpoint endpoint)
    {
        // Only check public endpoints
        if (endpoint.Classification != SecurityClassification.Public)
            return null;

        var route = endpoint.Route.ToLowerInvariant();
        var foundKeywords = SensitiveKeywords
            .Where(keyword => route.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (foundKeywords.Count == 0)
            return null;

        return new Finding
        {
            RuleId = RuleId,
            RuleName = Name,
            Severity = DefaultSeverity,
            Endpoint = endpoint,
            Message = $"Public endpoint '{endpoint.Route}' contains sensitive keywords: " +
                     $"{string.Join(", ", foundKeywords)}.",
            Recommendation = "Routes with sensitive keywords typically require authorization. " +
                           "Add [Authorize] attribute or move to a protected area of the API."
        };
    }
}
