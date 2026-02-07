using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiPosture.Core.Licensing;

namespace ApiPosture.Licensing;

/// <summary>
/// Simplified license manager for free CLI - only reads environment variable for CI/CD.
/// Activation/deactivation is handled by ApiPosturePro CLI tool.
/// </summary>
public sealed class LicenseManager : IDisposable
{
    private const string LicenseKeyEnvVar = "APIPOSTURE_LICENSE_KEY";
    private ILicenseContext? _cachedContext;
    private bool _disposed;

    /// <summary>
    /// Gets the current license context.
    /// Checks environment variable only (for CI/CD).
    /// </summary>
    public ILicenseContext GetLicenseContext()
    {
        if (_cachedContext != null)
        {
            return _cachedContext;
        }

        // Check environment variable for CI/CD
        var envKey = Environment.GetEnvironmentVariable(LicenseKeyEnvVar);
        if (!string.IsNullOrEmpty(envKey))
        {
            var context = ParseLicenseToken(envKey);
            _cachedContext = context;
            return _cachedContext;
        }

        _cachedContext = FreeLicenseContext.Instance;
        return _cachedContext;
    }

    private static ILicenseContext ParseLicenseToken(string token)
    {
        // JWT tokens have 3 parts separated by dots
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return FreeLicenseContext.Instance;
        }

        try
        {
            // Decode the payload (second part)
            var payload = parts[1];
            // Add padding if needed
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(payload));

            var claims = JsonSerializer.Deserialize<LicenseTokenClaims>(payloadJson);
            if (claims == null)
            {
                return FreeLicenseContext.Instance;
            }

            return new ProLicenseContext
            {
                Tier = claims.Tier ?? LicenseTier.Pro,
                Features = claims.Features ?? LicenseFeatures.AllProFeatures.ToList(),
                Organization = claims.Org,
                ExpiresAt = claims.Exp.HasValue
                    ? DateTimeOffset.FromUnixTimeSeconds(claims.Exp.Value)
                    : null,
                IsCiLicense = claims.Type == "ci",
                Seats = claims.Seats ?? 1
            };
        }
        catch
        {
            return FreeLicenseContext.Instance;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

/// <summary>
/// Pro license context implementation.
/// </summary>
internal sealed class ProLicenseContext : ILicenseContext
{
    public bool IsValid => true;
    public required string Tier { get; init; }
    public required IReadOnlyList<string> Features { get; init; }
    public string? Organization { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public bool IsCiLicense { get; init; }
    public int Seats { get; init; } = 1;

    public bool HasFeature(string feature) => Features.Contains(feature);
    public bool HasAllFeatures(IEnumerable<string> features) => features.All(HasFeature);
}

/// <summary>
/// JWT token claims for license validation.
/// </summary>
internal sealed class LicenseTokenClaims
{
    [JsonPropertyName("tier")]
    public string? Tier { get; set; }

    [JsonPropertyName("features")]
    public List<string>? Features { get; set; }

    [JsonPropertyName("org")]
    public string? Org { get; set; }

    [JsonPropertyName("seats")]
    public int? Seats { get; set; }

    [JsonPropertyName("exp")]
    public long? Exp { get; set; }

    [JsonPropertyName("machineId")]
    public string? MachineId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}
