namespace ApiPosture.Core.Configuration;

/// <summary>
/// Represents a suppression rule for filtering out findings.
/// </summary>
public sealed class Suppression
{
    /// <summary>
    /// Route pattern to match. Supports exact matches and wildcard (*) patterns.
    /// </summary>
    public string? Route { get; init; }

    /// <summary>
    /// List of rule IDs to suppress (e.g., ["AP001", "AP002"]).
    /// If empty or null, all rules are suppressed for the matched route.
    /// </summary>
    public string[]? Rules { get; init; }

    /// <summary>
    /// Reason for the suppression (for documentation purposes).
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Checks if this suppression matches the given route and rule ID.
    /// </summary>
    public bool Matches(string route, string ruleId)
    {
        if (!MatchesRoute(route))
            return false;

        if (Rules is null || Rules.Length == 0)
            return true;

        return Rules.Contains(ruleId, StringComparer.OrdinalIgnoreCase);
    }

    private bool MatchesRoute(string route)
    {
        if (string.IsNullOrEmpty(Route))
            return true;

        // Exact match
        if (Route.Equals(route, StringComparison.OrdinalIgnoreCase))
            return true;

        // Wildcard pattern matching
        if (Route.Contains('*'))
        {
            return MatchesWildcard(Route, route);
        }

        return false;
    }

    private static bool MatchesWildcard(string pattern, string value)
    {
        // Simple wildcard matching: * matches any sequence of characters
        var parts = pattern.Split('*');

        if (parts.Length == 1)
            return pattern.Equals(value, StringComparison.OrdinalIgnoreCase);

        var index = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (string.IsNullOrEmpty(part))
                continue;

            var foundIndex = value.IndexOf(part, index, StringComparison.OrdinalIgnoreCase);
            if (foundIndex < 0)
                return false;

            // First part must match at start (if pattern doesn't start with *)
            if (i == 0 && foundIndex != 0 && !pattern.StartsWith("*"))
                return false;

            // Last part must match at end (if pattern doesn't end with *)
            if (i == parts.Length - 1 && !pattern.EndsWith("*"))
            {
                if (foundIndex + part.Length != value.Length)
                    return false;
            }

            index = foundIndex + part.Length;
        }

        return true;
    }
}
