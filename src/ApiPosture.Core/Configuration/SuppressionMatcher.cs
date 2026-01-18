using ApiPosture.Core.Models;

namespace ApiPosture.Core.Configuration;

/// <summary>
/// Provides methods for matching findings against suppressions.
/// </summary>
public sealed class SuppressionMatcher
{
    private readonly IReadOnlyList<Suppression> _suppressions;

    public SuppressionMatcher(IReadOnlyList<Suppression>? suppressions)
    {
        _suppressions = suppressions ?? Array.Empty<Suppression>();
    }

    /// <summary>
    /// Checks if a finding should be suppressed.
    /// </summary>
    public bool IsSuppressed(Finding finding)
    {
        if (_suppressions.Count == 0)
            return false;

        return _suppressions.Any(s => s.Matches(finding.Endpoint.Route, finding.RuleId));
    }

    /// <summary>
    /// Filters out suppressed findings from the collection.
    /// </summary>
    public IEnumerable<Finding> FilterSuppressions(IEnumerable<Finding> findings)
    {
        if (_suppressions.Count == 0)
            return findings;

        return findings.Where(f => !IsSuppressed(f));
    }

    /// <summary>
    /// Creates a matcher from configuration.
    /// </summary>
    public static SuppressionMatcher FromConfig(ApiPostureConfig config)
    {
        return new SuppressionMatcher(config.Suppressions);
    }
}
