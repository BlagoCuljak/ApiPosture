using ApiPosture.Core.Models;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Core.Filtering;

/// <summary>
/// Configuration for filtering endpoints and findings.
/// </summary>
public sealed class FilterOptions
{
    /// <summary>
    /// Minimum severity to include.
    /// </summary>
    public Severity? MinSeverity { get; init; }

    /// <summary>
    /// Security classifications to include.
    /// </summary>
    public SecurityClassification[]? Classifications { get; init; }

    /// <summary>
    /// HTTP methods to include.
    /// </summary>
    public HttpMethod[]? Methods { get; init; }

    /// <summary>
    /// Substring to search for in routes (case-insensitive).
    /// </summary>
    public string? RouteContains { get; init; }

    /// <summary>
    /// Controller names to include.
    /// </summary>
    public string[]? Controllers { get; init; }

    /// <summary>
    /// API styles to include (Controller, MinimalApi).
    /// </summary>
    public EndpointType[]? ApiStyles { get; init; }

    /// <summary>
    /// Rule IDs to include.
    /// </summary>
    public string[]? Rules { get; init; }

    /// <summary>
    /// Gets whether any filters are configured.
    /// </summary>
    public bool HasFilters =>
        MinSeverity.HasValue ||
        Classifications is { Length: > 0 } ||
        Methods is { Length: > 0 } ||
        !string.IsNullOrEmpty(RouteContains) ||
        Controllers is { Length: > 0 } ||
        ApiStyles is { Length: > 0 } ||
        Rules is { Length: > 0 };

    /// <summary>
    /// Creates a default filter (no filtering).
    /// </summary>
    public static FilterOptions Default { get; } = new();

    /// <summary>
    /// Creates filter options from CLI string values.
    /// </summary>
    public static FilterOptions FromStrings(
        string? severity,
        string[]? classifications,
        string[]? methods,
        string? routeContains,
        string[]? controllers,
        string[]? apiStyles,
        string[]? rules)
    {
        return new FilterOptions
        {
            MinSeverity = ParseSeverity(severity),
            Classifications = ParseClassifications(classifications),
            Methods = ParseMethods(methods),
            RouteContains = routeContains,
            Controllers = controllers,
            ApiStyles = ParseApiStyles(apiStyles),
            Rules = rules
        };
    }

    private static Severity? ParseSeverity(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return value.ToLowerInvariant() switch
        {
            "info" => Severity.Info,
            "low" => Severity.Low,
            "medium" => Severity.Medium,
            "high" => Severity.High,
            "critical" => Severity.Critical,
            _ => null
        };
    }

    private static SecurityClassification[]? ParseClassifications(string[]? values)
    {
        if (values is null || values.Length == 0)
            return null;

        var results = new List<SecurityClassification>();
        foreach (var value in values)
        {
            var parsed = ParseClassification(value);
            if (parsed.HasValue)
                results.Add(parsed.Value);
        }

        return results.Count > 0 ? results.ToArray() : null;
    }

    private static SecurityClassification? ParseClassification(string value)
    {
        return value.ToLowerInvariant().Replace("-", "").Replace("_", "") switch
        {
            "public" => SecurityClassification.Public,
            "authenticated" or "auth" => SecurityClassification.Authenticated,
            "rolerestricted" or "role" => SecurityClassification.RoleRestricted,
            "policyrestricted" or "policy" => SecurityClassification.PolicyRestricted,
            _ => null
        };
    }

    private static HttpMethod[]? ParseMethods(string[]? values)
    {
        if (values is null || values.Length == 0)
            return null;

        var results = new List<HttpMethod>();
        foreach (var value in values)
        {
            var parsed = ParseMethod(value);
            if (parsed.HasValue)
                results.Add(parsed.Value);
        }

        return results.Count > 0 ? results.ToArray() : null;
    }

    private static HttpMethod? ParseMethod(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "HEAD" => HttpMethod.Head,
            "OPTIONS" => HttpMethod.Options,
            _ => null
        };
    }

    private static EndpointType[]? ParseApiStyles(string[]? values)
    {
        if (values is null || values.Length == 0)
            return null;

        var results = new List<EndpointType>();
        foreach (var value in values)
        {
            var parsed = ParseApiStyle(value);
            if (parsed.HasValue)
                results.Add(parsed.Value);
        }

        return results.Count > 0 ? results.ToArray() : null;
    }

    private static EndpointType? ParseApiStyle(string value)
    {
        return value.ToLowerInvariant().Replace("-", "").Replace("_", "") switch
        {
            "controller" or "ctrl" => EndpointType.Controller,
            "minimal" or "minimalapi" or "min" => EndpointType.MinimalApi,
            _ => null
        };
    }
}
