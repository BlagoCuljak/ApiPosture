using ApiPosture.Core.Models;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Core.Sorting;

/// <summary>
/// Provides sorting functionality for findings.
/// </summary>
public sealed class FindingSorter
{
    private readonly SortOptions _options;

    public FindingSorter(SortOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Sorts the given findings according to the configured options.
    /// </summary>
    public IEnumerable<Finding> Sort(IEnumerable<Finding> findings)
    {
        var sorted = _options.Field switch
        {
            SortField.Severity => SortBySeverity(findings),
            SortField.Route => SortByRoute(findings),
            SortField.Method => SortByMethod(findings),
            SortField.Classification => SortByClassification(findings),
            SortField.Controller => SortByController(findings),
            SortField.Location => SortByLocation(findings),
            _ => SortBySeverity(findings)
        };

        // Apply direction
        if (_options.Direction == SortDirection.Descending)
        {
            return sorted.Reverse();
        }

        return sorted;
    }

    private IOrderedEnumerable<Finding> SortBySeverity(IEnumerable<Finding> findings)
    {
        // Critical > High > Medium > Low > Info
        // Then by route for stable sorting
        return findings
            .OrderBy(f => GetSeverityOrder(f.Severity))
            .ThenBy(f => f.Endpoint.Route, StringComparer.OrdinalIgnoreCase);
    }

    private IOrderedEnumerable<Finding> SortByRoute(IEnumerable<Finding> findings)
    {
        return findings
            .OrderBy(f => f.Endpoint.Route, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => GetSeverityOrder(f.Severity));
    }

    private IOrderedEnumerable<Finding> SortByMethod(IEnumerable<Finding> findings)
    {
        return findings
            .OrderBy(f => GetMethodOrder(f.Endpoint.Methods))
            .ThenBy(f => f.Endpoint.Route, StringComparer.OrdinalIgnoreCase);
    }

    private IOrderedEnumerable<Finding> SortByClassification(IEnumerable<Finding> findings)
    {
        return findings
            .OrderBy(f => GetClassificationOrder(f.Endpoint.Classification))
            .ThenBy(f => f.Endpoint.Route, StringComparer.OrdinalIgnoreCase);
    }

    private IOrderedEnumerable<Finding> SortByController(IEnumerable<Finding> findings)
    {
        return findings
            .OrderBy(f => f.Endpoint.ControllerName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Endpoint.Route, StringComparer.OrdinalIgnoreCase);
    }

    private IOrderedEnumerable<Finding> SortByLocation(IEnumerable<Finding> findings)
    {
        return findings
            .OrderBy(f => f.Endpoint.Location.FilePath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(f => f.Endpoint.Location.LineNumber)
            .ThenBy(f => f.Endpoint.Route, StringComparer.OrdinalIgnoreCase);
    }

    private static int GetSeverityOrder(Severity severity)
    {
        // Lower number = higher severity (for ascending sort to show critical first)
        return severity switch
        {
            Severity.Critical => 0,
            Severity.High => 1,
            Severity.Medium => 2,
            Severity.Low => 3,
            Severity.Info => 4,
            _ => 5
        };
    }

    private static int GetClassificationOrder(SecurityClassification classification)
    {
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
    public static FindingSorter Default { get; } = new(SortOptions.Default);
}
