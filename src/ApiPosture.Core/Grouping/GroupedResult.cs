using ApiPosture.Core.Models;

namespace ApiPosture.Core.Grouping;

/// <summary>
/// Represents a group of endpoints with a common grouping key.
/// </summary>
public sealed class EndpointGroup
{
    /// <summary>
    /// The grouping key (e.g., controller name, classification name).
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Display name for the group header.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The endpoints in this group.
    /// </summary>
    public required IReadOnlyList<Endpoint> Endpoints { get; init; }

    /// <summary>
    /// The number of endpoints in this group.
    /// </summary>
    public int Count => Endpoints.Count;
}

/// <summary>
/// Represents a group of findings with a common grouping key.
/// </summary>
public sealed class FindingGroup
{
    /// <summary>
    /// The grouping key (e.g., severity name, controller name).
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// Display name for the group header.
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// The findings in this group.
    /// </summary>
    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>
    /// The number of findings in this group.
    /// </summary>
    public int Count => Findings.Count;
}

/// <summary>
/// Represents grouped scan results.
/// </summary>
public sealed class GroupedScanResult
{
    /// <summary>
    /// The original scan result.
    /// </summary>
    public required ScanResult ScanResult { get; init; }

    /// <summary>
    /// Endpoint groups (if grouping is enabled).
    /// </summary>
    public IReadOnlyList<EndpointGroup>? EndpointGroups { get; init; }

    /// <summary>
    /// Finding groups (if grouping is enabled).
    /// </summary>
    public IReadOnlyList<FindingGroup>? FindingGroups { get; init; }

    /// <summary>
    /// Gets whether endpoint grouping is active.
    /// </summary>
    public bool HasEndpointGroups => EndpointGroups is { Count: > 0 };

    /// <summary>
    /// Gets whether finding grouping is active.
    /// </summary>
    public bool HasFindingGroups => FindingGroups is { Count: > 0 };
}
