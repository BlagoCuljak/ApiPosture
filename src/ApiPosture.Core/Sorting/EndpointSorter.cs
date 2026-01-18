using ApiPosture.Core.Models;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Core.Sorting;

/// <summary>
/// Provides sorting functionality for endpoints.
/// </summary>
public sealed class EndpointSorter
{
    private readonly SortOptions _options;

    public EndpointSorter(SortOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Sorts the given endpoints according to the configured options.
    /// </summary>
    public IEnumerable<Endpoint> Sort(IEnumerable<Endpoint> endpoints)
    {
        var sorted = _options.Field switch
        {
            SortField.Severity => SortByClassification(endpoints),
            SortField.Classification => SortByClassification(endpoints),
            SortField.Route => SortByRoute(endpoints),
            SortField.Method => SortByMethod(endpoints),
            SortField.Controller => SortByController(endpoints),
            SortField.Location => SortByLocation(endpoints),
            _ => SortByClassification(endpoints)
        };

        // Apply direction
        if (_options.Direction == SortDirection.Descending)
        {
            return sorted.Reverse();
        }

        return sorted;
    }

    private IOrderedEnumerable<Endpoint> SortByClassification(IEnumerable<Endpoint> endpoints)
    {
        // Public (highest risk) > Authenticated > RoleRestricted > PolicyRestricted
        // Then by route for stable sorting
        return endpoints
            .OrderBy(e => GetClassificationOrder(e.Classification))
            .ThenBy(e => e.Route, StringComparer.OrdinalIgnoreCase);
    }

    private IOrderedEnumerable<Endpoint> SortByRoute(IEnumerable<Endpoint> endpoints)
    {
        return endpoints
            .OrderBy(e => e.Route, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => GetMethodOrder(e.Methods));
    }

    private IOrderedEnumerable<Endpoint> SortByMethod(IEnumerable<Endpoint> endpoints)
    {
        // GET < POST < PUT < DELETE < PATCH
        return endpoints
            .OrderBy(e => GetMethodOrder(e.Methods))
            .ThenBy(e => e.Route, StringComparer.OrdinalIgnoreCase);
    }

    private IOrderedEnumerable<Endpoint> SortByController(IEnumerable<Endpoint> endpoints)
    {
        return endpoints
            .OrderBy(e => e.ControllerName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Route, StringComparer.OrdinalIgnoreCase);
    }

    private IOrderedEnumerable<Endpoint> SortByLocation(IEnumerable<Endpoint> endpoints)
    {
        return endpoints
            .OrderBy(e => e.Location.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Location.LineNumber)
            .ThenBy(e => e.Route, StringComparer.OrdinalIgnoreCase);
    }

    private static int GetClassificationOrder(SecurityClassification classification)
    {
        // Lower number = higher risk (for ascending sort to show riskiest first)
        return classification switch
        {
            SecurityClassification.Public => 0,
            SecurityClassification.Authenticated => 1,
            SecurityClassification.RoleRestricted => 2,
            SecurityClassification.PolicyRestricted => 3,
            _ => 4
        };
    }

    private static int GetMethodOrder(HttpMethod methods)
    {
        // Order by primary method in the flags
        if (methods.HasFlag(HttpMethod.Get)) return 0;
        if (methods.HasFlag(HttpMethod.Post)) return 1;
        if (methods.HasFlag(HttpMethod.Put)) return 2;
        if (methods.HasFlag(HttpMethod.Delete)) return 3;
        if (methods.HasFlag(HttpMethod.Patch)) return 4;
        if (methods.HasFlag(HttpMethod.Head)) return 5;
        if (methods.HasFlag(HttpMethod.Options)) return 6;
        return 7;
    }

    /// <summary>
    /// Creates a sorter with default options.
    /// </summary>
    public static EndpointSorter Default { get; } = new(SortOptions.Default);
}
