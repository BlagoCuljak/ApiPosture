namespace ApiPosture.Core.Licensing;

/// <summary>
/// Result of a license validation operation.
/// </summary>
public sealed class LicenseValidationResult
{
    /// <summary>
    /// Whether the license is valid.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// The license context if validation succeeded.
    /// </summary>
    public ILicenseContext? License { get; init; }

    /// <summary>
    /// Error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Error code if validation failed.
    /// </summary>
    public LicenseErrorCode ErrorCode { get; init; }

    /// <summary>
    /// Whether the license is in offline grace period.
    /// </summary>
    public bool IsInGracePeriod { get; init; }

    /// <summary>
    /// Days remaining in grace period (if applicable).
    /// </summary>
    public int GracePeriodDaysRemaining { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static LicenseValidationResult Success(ILicenseContext license, bool isInGracePeriod = false, int graceDaysRemaining = 0) => new()
    {
        IsValid = true,
        License = license,
        ErrorCode = LicenseErrorCode.None,
        IsInGracePeriod = isInGracePeriod,
        GracePeriodDaysRemaining = graceDaysRemaining
    };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static LicenseValidationResult Failure(LicenseErrorCode code, string message) => new()
    {
        IsValid = false,
        ErrorCode = code,
        ErrorMessage = message
    };
}

/// <summary>
/// Error codes for license validation failures.
/// </summary>
public enum LicenseErrorCode
{
    /// <summary>
    /// No error.
    /// </summary>
    None = 0,

    /// <summary>
    /// The license key is invalid.
    /// </summary>
    InvalidKey = 1,

    /// <summary>
    /// The license has expired.
    /// </summary>
    Expired = 2,

    /// <summary>
    /// The machine ID doesn't match.
    /// </summary>
    MachineMismatch = 3,

    /// <summary>
    /// The license has been revoked.
    /// </summary>
    Revoked = 4,

    /// <summary>
    /// Too many activations for this license.
    /// </summary>
    TooManyActivations = 5,

    /// <summary>
    /// Network error during validation.
    /// </summary>
    NetworkError = 6,

    /// <summary>
    /// The offline grace period has expired.
    /// </summary>
    GracePeriodExpired = 7,

    /// <summary>
    /// The license token signature is invalid.
    /// </summary>
    InvalidSignature = 8
}
