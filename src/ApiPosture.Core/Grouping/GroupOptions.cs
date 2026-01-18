namespace ApiPosture.Core.Grouping;

/// <summary>
/// Configuration for grouping endpoints and findings.
/// </summary>
public sealed class GroupOptions
{
    /// <summary>
    /// The field to group endpoints by.
    /// </summary>
    public GroupField? EndpointGroupBy { get; init; }

    /// <summary>
    /// The field to group findings by.
    /// </summary>
    public GroupField? FindingGroupBy { get; init; }

    /// <summary>
    /// Gets whether endpoint grouping is enabled.
    /// </summary>
    public bool HasEndpointGrouping => EndpointGroupBy.HasValue;

    /// <summary>
    /// Gets whether finding grouping is enabled.
    /// </summary>
    public bool HasFindingGrouping => FindingGroupBy.HasValue;

    /// <summary>
    /// Creates a default grouping configuration (no grouping).
    /// </summary>
    public static GroupOptions Default { get; } = new();

    /// <summary>
    /// Creates GroupOptions from CLI string values.
    /// </summary>
    public static GroupOptions FromStrings(string? groupBy, string? groupFindingsBy)
    {
        return new GroupOptions
        {
            EndpointGroupBy = ParseGroupField(groupBy),
            FindingGroupBy = ParseGroupField(groupFindingsBy)
        };
    }

    private static GroupField? ParseGroupField(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return value.ToLowerInvariant() switch
        {
            "controller" => GroupField.Controller,
            "classification" => GroupField.Classification,
            "severity" => GroupField.Severity,
            "method" => GroupField.Method,
            "type" or "apistyle" => GroupField.Type,
            _ => null
        };
    }
}

/// <summary>
/// Available fields for grouping.
/// </summary>
public enum GroupField
{
    /// <summary>
    /// Group by controller name.
    /// </summary>
    Controller,

    /// <summary>
    /// Group by security classification.
    /// </summary>
    Classification,

    /// <summary>
    /// Group by severity level.
    /// </summary>
    Severity,

    /// <summary>
    /// Group by HTTP method.
    /// </summary>
    Method,

    /// <summary>
    /// Group by endpoint type (Controller or MinimalApi).
    /// </summary>
    Type
}
