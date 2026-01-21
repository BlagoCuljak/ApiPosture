using ApiPosture.Core.Models;

namespace ApiPosture.Core.Extensions;

/// <summary>
/// Interface for extensions that provide additional security rules.
/// </summary>
public interface IRuleProvider
{
    /// <summary>
    /// Gets the security rules provided by this extension.
    /// </summary>
    IReadOnlyList<IExtensionRule> GetRules();
}

/// <summary>
/// Interface for security rules provided by extensions.
/// Similar to ISecurityRule but defined in Core to avoid circular dependencies.
/// </summary>
public interface IExtensionRule
{
    /// <summary>
    /// Unique identifier for this rule (e.g., AP101).
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
    /// Category of this rule (e.g., "OWASP", "Secrets").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Evaluates the rule against an endpoint.
    /// </summary>
    /// <returns>A finding if the rule triggers, null otherwise.</returns>
    Finding? Evaluate(Endpoint endpoint);
}
