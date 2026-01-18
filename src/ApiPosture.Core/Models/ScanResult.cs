namespace ApiPosture.Core.Models;

/// <summary>
/// Represents the complete result of a security scan.
/// </summary>
public sealed class ScanResult
{
    /// <summary>
    /// Path that was scanned.
    /// </summary>
    public required string ScannedPath { get; init; }

    /// <summary>
    /// All discovered endpoints.
    /// </summary>
    public required IReadOnlyList<Endpoint> Endpoints { get; init; }

    /// <summary>
    /// All security findings.
    /// </summary>
    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>
    /// Files that were scanned.
    /// </summary>
    public required IReadOnlyList<string> ScannedFiles { get; init; }

    /// <summary>
    /// Files that could not be parsed.
    /// </summary>
    public required IReadOnlyList<string> FailedFiles { get; init; }

    /// <summary>
    /// Time taken for the scan.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets findings filtered by minimum severity.
    /// </summary>
    public IReadOnlyList<Finding> GetFindingsBySeverity(Severity minSeverity) =>
        Findings.Where(f => f.Severity >= minSeverity).ToList();

    /// <summary>
    /// Gets the highest severity finding, if any.
    /// </summary>
    public Severity? HighestSeverity =>
        Findings.Count > 0 ? Findings.Max(f => f.Severity) : null;
}
