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

    private static readonly string[] DefaultKeywords =
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

    private readonly string[] _sensitiveKeywords;

    /// <summary>
    /// Creates a new instance with default sensitive keywords.
    /// </summary>
    public SensitiveRouteKeywordsRule() : this(null)
    {
    }

    /// <summary>
    /// Creates a new instance with custom sensitive keywords.
    /// </summary>
    /// <param name="customKeywords">Custom keywords to use instead of defaults. Pass null to use defaults.</param>
    public SensitiveRouteKeywordsRule(string[]? customKeywords)
    {
        _sensitiveKeywords = customKeywords is { Length: > 0 } ? customKeywords : DefaultKeywords;
    }

    /// <summary>
    /// Gets the keywords currently being used for detection.
    /// </summary>
    public IReadOnlyList<string> Keywords => _sensitiveKeywords;

    public Finding? Evaluate(Endpoint endpoint)
    {
        // Only check public endpoints
        if (endpoint.Classification != SecurityClassification.Public)
            return null;

        var route = endpoint.Route.ToLowerInvariant();
        var segments = route.Split('/');
        var foundKeywords = _sensitiveKeywords
            .Where(keyword => segments.Any(s => s.Equals(keyword, StringComparison.OrdinalIgnoreCase)))
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
