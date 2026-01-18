namespace ApiPosture.Core.Sorting;

/// <summary>
/// Configuration for sorting endpoints and findings.
/// </summary>
public sealed class SortOptions
{
    /// <summary>
    /// The field to sort by.
    /// </summary>
    public SortField Field { get; init; } = SortField.Severity;

    /// <summary>
    /// The sort direction.
    /// </summary>
    public SortDirection Direction { get; init; } = SortDirection.Descending;

    /// <summary>
    /// Creates a default sorting configuration (severity descending).
    /// </summary>
    public static SortOptions Default { get; } = new();

    /// <summary>
    /// Creates SortOptions from CLI string values.
    /// </summary>
    public static SortOptions FromStrings(string? sortBy, string? sortDir)
    {
        return new SortOptions
        {
            Field = ParseField(sortBy),
            Direction = ParseDirection(sortDir)
        };
    }

    private static SortField ParseField(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return SortField.Severity;

        return value.ToLowerInvariant() switch
        {
            "severity" => SortField.Severity,
            "route" => SortField.Route,
            "method" => SortField.Method,
            "classification" => SortField.Classification,
            "controller" => SortField.Controller,
            "location" => SortField.Location,
            _ => SortField.Severity
        };
    }

    private static SortDirection ParseDirection(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return SortDirection.Descending;

        return value.ToLowerInvariant() switch
        {
            "asc" or "ascending" => SortDirection.Ascending,
            "desc" or "descending" => SortDirection.Descending,
            _ => SortDirection.Descending
        };
    }
}

/// <summary>
/// Available fields for sorting.
/// </summary>
public enum SortField
{
    /// <summary>
    /// Sort by severity level (for findings) or classification (for endpoints).
    /// </summary>
    Severity,

    /// <summary>
    /// Sort by route path alphabetically.
    /// </summary>
    Route,

    /// <summary>
    /// Sort by HTTP method (GET, POST, PUT, DELETE order).
    /// </summary>
    Method,

    /// <summary>
    /// Sort by security classification.
    /// </summary>
    Classification,

    /// <summary>
    /// Sort by controller name.
    /// </summary>
    Controller,

    /// <summary>
    /// Sort by source file location.
    /// </summary>
    Location
}

/// <summary>
/// Sort direction.
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// Sort in ascending order (A-Z, low-high).
    /// </summary>
    Ascending,

    /// <summary>
    /// Sort in descending order (Z-A, high-low).
    /// </summary>
    Descending
}
