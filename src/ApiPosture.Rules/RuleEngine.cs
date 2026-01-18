using ApiPosture.Core.Models;
using ApiPosture.Rules.Consistency;
using ApiPosture.Rules.Exposure;
using ApiPosture.Rules.Privilege;
using ApiPosture.Rules.Surface;

namespace ApiPosture.Rules;

/// <summary>
/// Orchestrates evaluation of all security rules against endpoints.
/// </summary>
public sealed class RuleEngine
{
    private readonly IReadOnlyList<ISecurityRule> _rules;

    public RuleEngine() : this(CreateDefaultRules())
    {
    }

    public RuleEngine(IReadOnlyList<ISecurityRule> rules)
    {
        _rules = rules;
    }

    /// <summary>
    /// Gets all registered rules.
    /// </summary>
    public IReadOnlyList<ISecurityRule> Rules => _rules;

    /// <summary>
    /// Evaluates all rules against the given endpoints.
    /// </summary>
    public IReadOnlyList<Finding> Evaluate(IEnumerable<Endpoint> endpoints)
    {
        var findings = new List<Finding>();

        foreach (var endpoint in endpoints)
        {
            foreach (var rule in _rules)
            {
                var finding = rule.Evaluate(endpoint);
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

        foreach (var rule in _rules)
        {
            var finding = rule.Evaluate(endpoint);
            if (finding != null)
            {
                findings.Add(finding);
            }
        }

        return findings;
    }

    private static IReadOnlyList<ISecurityRule> CreateDefaultRules()
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
            new SensitiveRouteKeywordsRule(),            // AP007
            new MinimalApiWithoutAuthRule()              // AP008
        };
    }
}
