using ApiPosture.Core.Models;

namespace ApiPosture.Core.Filtering;

/// <summary>
/// Provides filtering functionality for endpoints.
/// </summary>
public sealed class EndpointFilter
{
    private readonly FilterOptions _options;

    public EndpointFilter(FilterOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Filters endpoints according to the configured options.
    /// </summary>
    public IEnumerable<Endpoint> Filter(IEnumerable<Endpoint> endpoints)
    {
        if (!_options.HasFilters)
            return endpoints;

        return endpoints.Where(Matches);
    }

    /// <summary>
    /// Checks if an endpoint matches all filter criteria.
    /// </summary>
    public bool Matches(Endpoint endpoint)
    {
        // Classification filter
        if (_options.Classifications is { Length: > 0 })
        {
            if (!_options.Classifications.Contains(endpoint.Classification))
                return false;
        }

        // Methods filter
        if (_options.Methods is { Length: > 0 })
        {
            var hasMatchingMethod = false;
            foreach (var method in _options.Methods)
            {
                if (endpoint.Methods.HasFlag(method))
                {
                    hasMatchingMethod = true;
                    break;
                }
            }
            if (!hasMatchingMethod)
                return false;
        }

        // Route contains filter
        if (!string.IsNullOrEmpty(_options.RouteContains))
        {
            if (!endpoint.Route.Contains(_options.RouteContains, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Controller filter
        if (_options.Controllers is { Length: > 0 })
        {
            var matchesController = endpoint.ControllerName is not null &&
                _options.Controllers.Any(c =>
                    c.Equals(endpoint.ControllerName, StringComparison.OrdinalIgnoreCase) ||
                    c.Equals(endpoint.ControllerName.Replace("Controller", ""), StringComparison.OrdinalIgnoreCase));

            if (!matchesController)
                return false;
        }

        // API style filter
        if (_options.ApiStyles is { Length: > 0 })
        {
            if (!_options.ApiStyles.Contains(endpoint.Type))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Creates a filter with default options (no filtering).
    /// </summary>
    public static EndpointFilter Default { get; } = new(FilterOptions.Default);
}
