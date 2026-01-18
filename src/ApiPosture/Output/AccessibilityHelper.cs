using ApiPosture.Core.Models;

namespace ApiPosture.Output;

/// <summary>
/// Provides accessibility options for terminal output including
/// color-free and icon-free alternatives.
/// </summary>
public sealed class AccessibilityHelper
{
    private readonly bool _useColors;
    private readonly bool _useIcons;

    public AccessibilityHelper(bool useColors = true, bool useIcons = true)
    {
        _useColors = useColors;
        _useIcons = useIcons;
    }

    /// <summary>
    /// Gets whether colors should be used in output.
    /// </summary>
    public bool UseColors => _useColors;

    /// <summary>
    /// Gets whether icons should be used in output.
    /// </summary>
    public bool UseIcons => _useIcons;

    /// <summary>
    /// Creates an AccessibilityHelper based on CLI flags and environment.
    /// </summary>
    public static AccessibilityHelper Create(bool noColorFlag, bool noIconsFlag, bool? configUseColors = null, bool? configUseIcons = null)
    {
        var useColors = DetermineUseColors(noColorFlag, configUseColors);
        var useIcons = DetermineUseIcons(noIconsFlag, configUseIcons);
        return new AccessibilityHelper(useColors, useIcons);
    }

    private static bool DetermineUseColors(bool noColorFlag, bool? configUseColors)
    {
        // CLI flag takes highest priority
        if (noColorFlag)
            return false;

        // NO_COLOR environment variable (https://no-color.org/)
        var noColorEnv = Environment.GetEnvironmentVariable("NO_COLOR");
        if (!string.IsNullOrEmpty(noColorEnv))
            return false;

        // Auto-detect if output is redirected
        if (Console.IsOutputRedirected)
            return false;

        // Config file setting
        if (configUseColors.HasValue)
            return configUseColors.Value;

        // Default: use colors
        return true;
    }

    private static bool DetermineUseIcons(bool noIconsFlag, bool? configUseIcons)
    {
        // CLI flag takes highest priority
        if (noIconsFlag)
            return false;

        // Config file setting
        if (configUseIcons.HasValue)
            return configUseIcons.Value;

        // Default: use icons
        return true;
    }

    /// <summary>
    /// Gets the severity indicator (icon or text) based on accessibility settings.
    /// </summary>
    public string GetSeverityIndicator(Severity severity)
    {
        if (_useIcons)
        {
            return severity switch
            {
                Severity.Critical => "\u274c",  // Red X
                Severity.High => "\u26a0\ufe0f",      // Warning sign
                Severity.Medium => "\u26a1",    // Lightning bolt
                Severity.Low => "\u2139\ufe0f",       // Info
                Severity.Info => "\u2139\ufe0f",
                _ => "\u2753"                   // Question mark
            };
        }

        return severity switch
        {
            Severity.Critical => "[CRIT]",
            Severity.High => "[HIGH]",
            Severity.Medium => "[MED]",
            Severity.Low => "[LOW]",
            Severity.Info => "[INFO]",
            _ => "[???]"
        };
    }

    /// <summary>
    /// Gets the classification indicator (icon or text) based on accessibility settings.
    /// </summary>
    public string GetClassificationIndicator(SecurityClassification classification)
    {
        if (_useIcons)
        {
            return classification switch
            {
                SecurityClassification.Public => "\ud83d\udd13",        // Unlocked
                SecurityClassification.Authenticated => "\ud83d\udd10", // Lock with key
                SecurityClassification.RoleRestricted => "\ud83d\udd12", // Locked
                SecurityClassification.PolicyRestricted => "\ud83d\udee1\ufe0f", // Shield
                _ => "\u2753"
            };
        }

        return classification switch
        {
            SecurityClassification.Public => "[PUBLIC]",
            SecurityClassification.Authenticated => "[AUTH]",
            SecurityClassification.RoleRestricted => "[ROLE]",
            SecurityClassification.PolicyRestricted => "[POLICY]",
            _ => "[???]"
        };
    }

    /// <summary>
    /// Gets the endpoint type indicator (icon or text) based on accessibility settings.
    /// </summary>
    public string GetEndpointTypeIndicator(EndpointType type)
    {
        if (_useIcons)
        {
            return type switch
            {
                EndpointType.Controller => "\ud83c\udfaf",  // Target/bullseye
                EndpointType.MinimalApi => "\u26a1",        // Lightning bolt
                _ => "\u2753"
            };
        }

        return type switch
        {
            EndpointType.Controller => "[CTRL]",
            EndpointType.MinimalApi => "[MIN]",
            _ => "[???]"
        };
    }

    /// <summary>
    /// Gets a checkmark or success indicator.
    /// </summary>
    public string GetSuccessIndicator()
    {
        return _useIcons ? "\u2705" : "[OK]";
    }

    /// <summary>
    /// Gets a cross or failure indicator.
    /// </summary>
    public string GetFailureIndicator()
    {
        return _useIcons ? "\u274c" : "[FAIL]";
    }

    /// <summary>
    /// Gets a warning indicator.
    /// </summary>
    public string GetWarningIndicator()
    {
        return _useIcons ? "\u26a0\ufe0f" : "[WARN]";
    }

    /// <summary>
    /// Applies color markup to text if colors are enabled.
    /// </summary>
    public string ApplyColor(string text, string color)
    {
        if (!_useColors)
            return text;

        return $"[{color}]{text}[/]";
    }

    /// <summary>
    /// Gets the Spectre.Console color for a severity level.
    /// </summary>
    public string GetSeverityColor(Severity severity)
    {
        return severity switch
        {
            Severity.Critical => "red bold",
            Severity.High => "orangered1",
            Severity.Medium => "yellow",
            Severity.Low => "blue",
            Severity.Info => "grey",
            _ => "white"
        };
    }

    /// <summary>
    /// Gets the Spectre.Console color for a classification.
    /// </summary>
    public string GetClassificationColor(SecurityClassification classification)
    {
        return classification switch
        {
            SecurityClassification.Public => "red",
            SecurityClassification.Authenticated => "yellow",
            SecurityClassification.RoleRestricted => "green",
            SecurityClassification.PolicyRestricted => "blue",
            _ => "white"
        };
    }
}
