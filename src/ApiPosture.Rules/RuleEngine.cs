using ApiPosture.Core.Extensions;
using ApiPosture.Core.Models;
using ApiPosture.Rules.Consistency;
using ApiPosture.Rules.Exposure;
using ApiPosture.Rules.Privilege;
using ApiPosture.Rules.Surface;

namespace ApiPosture.Rules;

/// <summary>
/// Configuration options for the rule engine.
/// </summary>
public sealed class RuleEngineConfig
{
    /// <summary>
    /// Custom sensitive keywords for AP007 rule. If null, default keywords are used.
    /// </summary>
    public string[]? SensitiveKeywords { get; init; }

    /// <summary>
    /// Extension rules to include in evaluation.
    /// </summary>
    public IEnumerable<IExtensionRule>? ExtensionRules { get; init; }

    /// <summary>
    /// Gets the default configuration.
    /// </summary>
    public static RuleEngineConfig Default { get; } = new();
}

/// <summary>
/// Orchestrates evaluation of all security rules against endpoints.
/// </summary>
public sealed class RuleEngine
{
    private readonly IReadOnlyList<ISecurityRule> _rules;
    private readonly IReadOnlyList<IExtensionRule> _extensionRules;

    public RuleEngine() : this(CreateDefaultRules(RuleEngineConfig.Default), Array.Empty<IExtensionRule>())
    {
    }

    public RuleEngine(RuleEngineConfig config) : this(CreateDefaultRules(config), config.ExtensionRules?.ToList() ?? (IReadOnlyList<IExtensionRule>)Array.Empty<IExtensionRule>())
    {
    }

    public RuleEngine(IReadOnlyList<ISecurityRule> rules) : this(rules, Array.Empty<IExtensionRule>())
    {
    }

    public RuleEngine(IReadOnlyList<ISecurityRule> rules, IReadOnlyList<IExtensionRule> extensionRules)
    {
        _rules = rules;
        _extensionRules = extensionRules;
    }

    /// <summary>
    /// Gets all registered core rules.
    /// </summary>
    public IReadOnlyList<ISecurityRule> Rules => _rules;

    /// <summary>
    /// Gets all extension rules.
    /// </summary>
    public IReadOnlyList<IExtensionRule> ExtensionRules => _extensionRules;

    /// <summary>
    /// Gets the total count of all rules (core + extension).
    /// </summary>
    public int TotalRuleCount => _rules.Count + _extensionRules.Count;

    /// <summary>
    /// Evaluates all rules against the given endpoints.
    /// </summary>
    public IReadOnlyList<Finding> Evaluate(IEnumerable<Endpoint> endpoints)
    {
        var findings = new List<Finding>();

        foreach (var endpoint in endpoints)
        {
            // Evaluate core rules
            foreach (var rule in _rules)
            {
                var finding = rule.Evaluate(endpoint);
                if (finding != null)
                {
                    findings.Add(finding);
                }
            }

            // Evaluate extension rules
            foreach (var extensionRule in _extensionRules)
            {
                var finding = extensionRule.Evaluate(endpoint);
                if (finding != null)
                {
                    findings.Add(finding);
                }
            }
        }

        return findings;
    }

    /// <summary>
    /// Evaluates all rules against a single endpoint.
    /// </summary>
    public IReadOnlyList<Finding> EvaluateEndpoint(Endpoint endpoint)
    {
        var findings = new List<Finding>();

        // Evaluate core rules
        foreach (var rule in _rules)
        {
            var finding = rule.Evaluate(endpoint);
            if (finding != null)
            {
                findings.Add(finding);
            }
        }

        // Evaluate extension rules
        foreach (var extensionRule in _extensionRules)
        {
            var finding = extensionRule.Evaluate(endpoint);
            if (finding != null)
            {
                findings.Add(finding);
            }
        }

        return findings;
    }

    private static IReadOnlyList<ISecurityRule> CreateDefaultRules(RuleEngineConfig config)
    {
        return new ISecurityRule[]
        {
            // Exposure rules
            new PublicWithoutExplicitIntentRule(),      // AP001
            new AllowAnonymousOnWriteRule(),             // AP002

            // Consistency rules
            new ControllerActionConflictRule(),          // AP003
            new MissingAuthOnWritesRule(),               // AP004

            // Privilege rules
            new ExcessiveRoleAccessRule(),               // AP005
            new WeakRoleNamingRule(),                    // AP006

            // Surface rules
            new SensitiveRouteKeywordsRule(config.SensitiveKeywords),  // AP007
            new MinimalApiWithoutAuthRule()              // AP008
        };
    }
}
