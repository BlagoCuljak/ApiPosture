using ApiPosture.Core.Models;

namespace ApiPosture.Rules;

/// <summary>
/// Interface for security rules that analyze endpoints.
/// </summary>
public interface ISecurityRule
{
    /// <summary>
    /// Unique identifier for this rule (e.g., AP001).
    /// </summary>
    string RuleId { get; }

    /// <summary>
    /// Human-readable name for this rule.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Default severity for findings from this rule.
    /// </summary>
    Severity DefaultSeverity { get; }

    /// <summary>
    /// Evaluates the rule against an endpoint.
    /// </summary>
    /// <returns>A finding if the rule triggers, null otherwise.</returns>
    Finding? Evaluate(Endpoint endpoint);
}
