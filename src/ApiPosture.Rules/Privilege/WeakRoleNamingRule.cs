using ApiPosture.Core.Models;

namespace ApiPosture.Rules.Privilege;

/// <summary>
/// AP006: Detects weak or generic role names like "User" or "Admin".
/// </summary>
public sealed class WeakRoleNamingRule : ISecurityRule
{
    public string RuleId => "AP006";
    public string Name => "Weak role naming";
    public Severity DefaultSeverity => Severity.Low;

    private static readonly HashSet<string> WeakRoleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "User",
        "Admin",
        "Administrator",
        "Guest",
        "Member",
        "Manager",
        "SuperUser",
        "Power User",
        "Basic",
        "Standard",
        "Premium"
    };

    public Finding? Evaluate(Endpoint endpoint)
    {
        var effectiveRoles = endpoint.Authorization.EffectiveRoles;
        var weakRoles = effectiveRoles
            .Where(r => WeakRoleNames.Contains(r))
            .ToList();

        if (weakRoles.Count == 0)
            return null;

        return new Finding
        {
            RuleId = RuleId,
            RuleName = Name,
            Severity = DefaultSeverity,
            Endpoint = endpoint,
            Message = $"Endpoint '{endpoint.Route}' uses generic role names: {string.Join(", ", weakRoles)}.",
            Recommendation = "Use more descriptive role names that indicate specific capabilities " +
                           "(e.g., 'OrderManager' instead of 'Admin', 'ReportViewer' instead of 'User')."
        };
    }
}
