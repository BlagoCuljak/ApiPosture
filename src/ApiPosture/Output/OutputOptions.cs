using ApiPosture.Core.Filtering;
using ApiPosture.Core.Grouping;
using ApiPosture.Core.Models;
using ApiPosture.Core.Sorting;

namespace ApiPosture.Output;

/// <summary>
/// Unified options for output formatting including filtering, sorting, grouping, and accessibility.
/// </summary>
public sealed class OutputOptions
{
    /// <summary>
    /// Minimum severity to include in output.
    /// </summary>
    public Severity MinSeverity { get; init; } = Severity.Info;

    /// <summary>
    /// Sorting options for endpoints and findings.
    /// </summary>
    public SortOptions SortOptions { get; init; } = SortOptions.Default;

    /// <summary>
    /// Filtering options for endpoints and findings.
    /// </summary>
    public FilterOptions FilterOptions { get; init; } = FilterOptions.Default;

    /// <summary>
    /// Grouping options for endpoints and findings.
    /// </summary>
    public GroupOptions GroupOptions { get; init; } = GroupOptions.Default;

    /// <summary>
    /// Accessibility helper for color and icon options.
    /// </summary>
    public AccessibilityHelper Accessibility { get; init; } = new();

    /// <summary>
    /// Gets whether colors should be used in output.
    /// </summary>
    public bool UseColors => Accessibility.UseColors;

    /// <summary>
    /// Gets whether icons should be used in output.
    /// </summary>
    public bool UseIcons => Accessibility.UseIcons;

    /// <summary>
    /// Creates a default OutputOptions instance.
    /// </summary>
    public static OutputOptions Default { get; } = new();

    /// <summary>
    /// Creates OutputOptions from individual settings.
    /// </summary>
    public static OutputOptions Create(
        Severity minSeverity = Severity.Info,
        SortOptions? sortOptions = null,
        FilterOptions? filterOptions = null,
        GroupOptions? groupOptions = null,
        AccessibilityHelper? accessibility = null)
    {
        return new OutputOptions
        {
            MinSeverity = minSeverity,
            SortOptions = sortOptions ?? SortOptions.Default,
            FilterOptions = filterOptions ?? FilterOptions.Default,
            GroupOptions = groupOptions ?? GroupOptions.Default,
            Accessibility = accessibility ?? new AccessibilityHelper()
        };
    }
}
