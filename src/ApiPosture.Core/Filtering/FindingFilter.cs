using ApiPosture.Core.Models;

namespace ApiPosture.Core.Filtering;

/// <summary>
/// Provides filtering functionality for findings.
/// </summary>
public sealed class FindingFilter
{
    private readonly FilterOptions _options;
    private readonly EndpointFilter _endpointFilter;

    public FindingFilter(FilterOptions options)
    {
        _options = options;
        _endpointFilter = new EndpointFilter(options);
    }

    /// <summary>
    /// Filters findings according to the configured options.
    /// </summary>
    public IEnumerable<Finding> Filter(IEnumerable<Finding> findings)
    {
        if (!_options.HasFilters && _options.MinSeverity is null && _options.Rules is null)
            return findings;

        return findings.Where(Matches);
    }

    /// <summary>
    /// Checks if a finding matches all filter criteria.
    /// </summary>
    public bool Matches(Finding finding)
    {
        // Severity filter
        if (_options.MinSeverity.HasValue)
        {
            if (finding.Severity < _options.MinSeverity.Value)
                return false;
        }

        // Rule filter
        if (_options.Rules is { Length: > 0 })
        {
            if (!_options.Rules.Any(r => r.Equals(finding.RuleId, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        // Endpoint-level filters
        if (!_endpointFilter.Matches(finding.Endpoint))
            return false;

        return true;
    }

    /// <summary>
    /// Creates a filter with default options (no filtering).
    /// </summary>
    public static FindingFilter Default { get; } = new(FilterOptions.Default);
}
