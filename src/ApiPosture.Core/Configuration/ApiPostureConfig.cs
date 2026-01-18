using ApiPosture.Core.Models;

namespace ApiPosture.Core.Configuration;

/// <summary>
/// Root configuration model for .apiposture.json files.
/// </summary>
public sealed class ApiPostureConfig
{
    /// <summary>
    /// Severity settings for filtering and exit code control.
    /// </summary>
    public SeverityConfig? Severity { get; init; }

    /// <summary>
    /// List of suppressions to filter out specific findings.
    /// </summary>
    public Suppression[]? Suppressions { get; init; }

    /// <summary>
    /// Rule-specific configuration overrides.
    /// </summary>
    public Dictionary<string, RuleConfig>? Rules { get; init; }

    /// <summary>
    /// Display settings for output formatting.
    /// </summary>
    public DisplayConfig? Display { get; init; }

    /// <summary>
    /// Checks if a finding should be suppressed based on the configured suppressions.
    /// </summary>
    public bool IsSuppressed(string route, string ruleId)
    {
        if (Suppressions is null || Suppressions.Length == 0)
            return false;

        return Suppressions.Any(s => s.Matches(route, ruleId));
    }

    /// <summary>
    /// Gets custom keywords for the SensitiveRouteKeywords rule (AP007).
    /// </summary>
    public string[]? GetSensitiveKeywords()
    {
        if (Rules is null)
            return null;

        if (Rules.TryGetValue("AP007", out var ruleConfig))
            return ruleConfig.SensitiveKeywords;

        return null;
    }

    /// <summary>
    /// Gets the default empty configuration.
    /// </summary>
    public static ApiPostureConfig Empty { get; } = new();
}

/// <summary>
/// Configuration for severity thresholds.
/// </summary>
public sealed class SeverityConfig
{
    /// <summary>
    /// Minimum severity to include in output (default: Info).
    /// </summary>
    public string? Default { get; init; }

    /// <summary>
    /// Severity threshold that causes a non-zero exit code.
    /// </summary>
    public string? FailOn { get; init; }

    /// <summary>
    /// Parses the default severity setting.
    /// </summary>
    public Models.Severity GetDefaultSeverity()
    {
        return ParseSeverity(Default) ?? Models.Severity.Info;
    }

    /// <summary>
    /// Parses the fail-on severity setting.
    /// </summary>
    public Models.Severity? GetFailOnSeverity()
    {
        return ParseSeverity(FailOn);
    }

    private static Models.Severity? ParseSeverity(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return value.ToLowerInvariant() switch
        {
            "info" => Models.Severity.Info,
            "low" => Models.Severity.Low,
            "medium" => Models.Severity.Medium,
            "high" => Models.Severity.High,
            "critical" => Models.Severity.Critical,
            _ => null
        };
    }
}

/// <summary>
/// Rule-specific configuration.
/// </summary>
public sealed class RuleConfig
{
    /// <summary>
    /// Whether the rule is enabled (default: true).
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Custom severity override for the rule.
    /// </summary>
    public string? Severity { get; init; }

    /// <summary>
    /// Custom sensitive keywords for AP007 rule.
    /// </summary>
    public string[]? SensitiveKeywords { get; init; }
}

/// <summary>
/// Display and output settings.
/// </summary>
public sealed class DisplayConfig
{
    /// <summary>
    /// Whether to use colors in terminal output (default: true).
    /// </summary>
    public bool UseColors { get; init; } = true;

    /// <summary>
    /// Whether to use icons/emojis in output (default: true).
    /// </summary>
    public bool UseIcons { get; init; } = true;
}
