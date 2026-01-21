namespace ApiPosture.Core.Licensing;

/// <summary>
/// Provides information about the current license.
/// </summary>
public interface ILicenseContext
{
    /// <summary>
    /// Whether the license is valid.
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// The license tier (e.g., "Free", "Pro").
    /// </summary>
    string Tier { get; }

    /// <summary>
    /// The licensed features.
    /// </summary>
    IReadOnlyList<string> Features { get; }

    /// <summary>
    /// Organization name on the license.
    /// </summary>
    string? Organization { get; }

    /// <summary>
    /// License expiration date (null for perpetual licenses).
    /// </summary>
    DateTimeOffset? ExpiresAt { get; }

    /// <summary>
    /// Whether the license is for CI/CD use (no machine binding).
    /// </summary>
    bool IsCiLicense { get; }

    /// <summary>
    /// Number of seats on the license.
    /// </summary>
    int Seats { get; }

    /// <summary>
    /// Whether a specific feature is enabled.
    /// </summary>
    bool HasFeature(string feature);

    /// <summary>
    /// Whether all the specified features are enabled.
    /// </summary>
    bool HasAllFeatures(IEnumerable<string> features);
}

/// <summary>
/// License tiers.
/// </summary>
public static class LicenseTier
{
    /// <summary>
    /// Free/OSS tier with core features only.
    /// </summary>
    public const string Free = "Free";

    /// <summary>
    /// Pro tier with all paid features.
    /// </summary>
    public const string Pro = "Pro";
}

/// <summary>
/// Available license features for the Pro tier.
/// </summary>
public static class LicenseFeatures
{
    /// <summary>
    /// Diff mode for comparing scans.
    /// </summary>
    public const string DiffMode = "diff";

    /// <summary>
    /// Historical tracking with SQLite storage.
    /// </summary>
    public const string HistoricalTracking = "history";

    /// <summary>
    /// Risk change scoring.
    /// </summary>
    public const string RiskScoring = "risk-scoring";

    /// <summary>
    /// Advanced OWASP rules (AP101-AP108).
    /// </summary>
    public const string AdvancedOwaspRules = "owasp-rules";

    /// <summary>
    /// Secrets scanning.
    /// </summary>
    public const string SecretsScanning = "secrets";

    /// <summary>
    /// All Pro features.
    /// </summary>
    public static readonly IReadOnlyList<string> AllProFeatures = new[]
    {
        DiffMode,
        HistoricalTracking,
        RiskScoring,
        AdvancedOwaspRules,
        SecretsScanning
    };
}

/// <summary>
/// Implementation of ILicenseContext for the free tier.
/// </summary>
public sealed class FreeLicenseContext : ILicenseContext
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static FreeLicenseContext Instance { get; } = new();

    private FreeLicenseContext() { }

    /// <inheritdoc />
    public bool IsValid => true;

    /// <inheritdoc />
    public string Tier => LicenseTier.Free;

    /// <inheritdoc />
    public IReadOnlyList<string> Features => Array.Empty<string>();

    /// <inheritdoc />
    public string? Organization => null;

    /// <inheritdoc />
    public DateTimeOffset? ExpiresAt => null;

    /// <inheritdoc />
    public bool IsCiLicense => false;

    /// <inheritdoc />
    public int Seats => 1;

    /// <inheritdoc />
    public bool HasFeature(string feature) => false;

    /// <inheritdoc />
    public bool HasAllFeatures(IEnumerable<string> features) => !features.Any();
}
