namespace ApiPosture.Core.Models;

/// <summary>
/// Represents a security finding from analysis.
/// </summary>
public sealed class Finding
{
    /// <summary>
    /// Unique rule identifier (e.g., AP001).
    /// </summary>
    public required string RuleId { get; init; }

    /// <summary>
    /// Human-readable rule name.
    /// </summary>
    public required string RuleName { get; init; }

    /// <summary>
    /// Severity level of the finding.
    /// </summary>
    public required Severity Severity { get; init; }

    /// <summary>
    /// Detailed message describing the finding.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The endpoint associated with this finding.
    /// </summary>
    public required Endpoint Endpoint { get; init; }

    /// <summary>
    /// Recommendation for addressing the finding.
    /// </summary>
    public required string Recommendation { get; init; }

    public override string ToString() => $"[{RuleId}] {Severity}: {Message}";
}
