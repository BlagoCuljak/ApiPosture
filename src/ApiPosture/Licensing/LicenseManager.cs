using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ApiPosture.Core.Licensing;

namespace ApiPosture.Licensing;

/// <summary>
/// Manages license activation, validation, and storage.
/// </summary>
public sealed class LicenseManager : IDisposable
{
    private const string LicenseFileName = "license.json";
    private const string LicenseServerBaseUrl = "https://apiposture.com/api/license";
    private const string LicenseKeyEnvVar = "APIPOSTURE_LICENSE_KEY";
    private const int GracePeriodDays = 7;

    private readonly string _dataDirectory;
    private readonly HttpClient _httpClient;
    private ILicenseContext? _cachedContext;
    private bool _disposed;

    /// <summary>
    /// Creates a new license manager.
    /// </summary>
    /// <param name="dataDirectory">Directory for storing license data (~/.apiposture).</param>
    public LicenseManager(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// Gets the path to the license file.
    /// </summary>
    public string LicenseFilePath => Path.Combine(_dataDirectory, LicenseFileName);

    /// <summary>
    /// Gets the current license context.
    /// Checks environment variable first, then falls back to stored license.
    /// </summary>
    public async Task<ILicenseContext> GetLicenseContextAsync()
    {
        if (_cachedContext != null)
        {
            return _cachedContext;
        }

        // Check environment variable first (for CI/CD)
        var envKey = Environment.GetEnvironmentVariable(LicenseKeyEnvVar);
        if (!string.IsNullOrEmpty(envKey))
        {
            var result = await ValidateKeyAsync(envKey);
            if (result.IsValid && result.License != null)
            {
                _cachedContext = result.License;
                return _cachedContext;
            }
        }

        // Try to load stored license
        var storedLicense = LoadStoredLicense();
        if (storedLicense != null)
        {
            var validationResult = await ValidateStoredLicenseAsync(storedLicense);
            _cachedContext = validationResult.License ?? FreeLicenseContext.Instance;
            return _cachedContext;
        }

        _cachedContext = FreeLicenseContext.Instance;
        return _cachedContext;
    }

    /// <summary>
    /// Synchronous version of GetLicenseContextAsync for simpler use cases.
    /// </summary>
    public ILicenseContext GetLicenseContext()
    {
        return GetLicenseContextAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Activates a license key.
    /// </summary>
    /// <param name="licenseKey">The license key to activate.</param>
    /// <returns>The activation result.</returns>
    public async Task<LicenseActivationResult> ActivateAsync(string licenseKey)
    {
        try
        {
            var machineId = GetMachineId();
            var request = new LicenseActivationRequest
            {
                LicenseKey = licenseKey,
                MachineId = machineId
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{LicenseServerBaseUrl}/activate",
                request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return new LicenseActivationResult
                {
                    Success = false,
                    ErrorMessage = $"Activation failed: {errorContent}"
                };
            }

            var activationResponse = await response.Content.ReadFromJsonAsync<LicenseActivationResponse>();
            if (activationResponse == null)
            {
                return new LicenseActivationResult
                {
                    Success = false,
                    ErrorMessage = "Invalid response from license server"
                };
            }

            // Store the license
            var storedLicense = new StoredLicense
            {
                Token = activationResponse.Token,
                LicenseKey = licenseKey,
                ActivatedAt = DateTimeOffset.UtcNow,
                LastValidatedAt = DateTimeOffset.UtcNow,
                MachineId = machineId
            };

            SaveStoredLicense(storedLicense);

            // Parse and cache the license context
            var context = ParseLicenseToken(activationResponse.Token);
            _cachedContext = context;

            return new LicenseActivationResult
            {
                Success = true,
                License = context,
                Message = "License activated successfully"
            };
        }
        catch (HttpRequestException ex)
        {
            return new LicenseActivationResult
            {
                Success = false,
                ErrorMessage = $"Network error during activation: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            return new LicenseActivationResult
            {
                Success = false,
                ErrorMessage = $"Activation error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Deactivates the current license.
    /// </summary>
    public async Task<LicenseDeactivationResult> DeactivateAsync()
    {
        var storedLicense = LoadStoredLicense();
        if (storedLicense == null)
        {
            return new LicenseDeactivationResult
            {
                Success = false,
                ErrorMessage = "No active license found"
            };
        }

        try
        {
            var request = new LicenseDeactivationRequest
            {
                Token = storedLicense.Token,
                MachineId = storedLicense.MachineId
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{LicenseServerBaseUrl}/deactivate",
                request);

            // Remove local license file regardless of server response
            if (File.Exists(LicenseFilePath))
            {
                File.Delete(LicenseFilePath);
            }
            _cachedContext = FreeLicenseContext.Instance;

            if (!response.IsSuccessStatusCode)
            {
                return new LicenseDeactivationResult
                {
                    Success = true,
                    Message = "Local license removed (server notification may have failed)"
                };
            }

            return new LicenseDeactivationResult
            {
                Success = true,
                Message = "License deactivated successfully"
            };
        }
        catch (HttpRequestException)
        {
            // Remove local license even if server is unreachable
            if (File.Exists(LicenseFilePath))
            {
                File.Delete(LicenseFilePath);
            }
            _cachedContext = FreeLicenseContext.Instance;

            return new LicenseDeactivationResult
            {
                Success = true,
                Message = "Local license removed (server unreachable)"
            };
        }
    }

    /// <summary>
    /// Gets the current license status.
    /// </summary>
    public async Task<LicenseStatus> GetStatusAsync()
    {
        var storedLicense = LoadStoredLicense();
        var context = await GetLicenseContextAsync();

        return new LicenseStatus
        {
            License = context,
            IsFromEnvironment = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(LicenseKeyEnvVar)),
            ActivatedAt = storedLicense?.ActivatedAt,
            LastValidatedAt = storedLicense?.LastValidatedAt
        };
    }

    private async Task<LicenseValidationResult> ValidateKeyAsync(string licenseKey)
    {
        try
        {
            var machineId = GetMachineId();
            var request = new LicenseValidationRequest
            {
                LicenseKey = licenseKey,
                MachineId = machineId
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{LicenseServerBaseUrl}/validate",
                request);

            if (!response.IsSuccessStatusCode)
            {
                return LicenseValidationResult.Failure(
                    LicenseErrorCode.InvalidKey,
                    "License key validation failed");
            }

            var validationResponse = await response.Content.ReadFromJsonAsync<LicenseValidationResponse>();
            if (validationResponse == null || !validationResponse.Valid)
            {
                return LicenseValidationResult.Failure(
                    LicenseErrorCode.InvalidKey,
                    validationResponse?.Error ?? "Invalid license");
            }

            var context = ParseLicenseToken(validationResponse.Token ?? string.Empty);
            return LicenseValidationResult.Success(context);
        }
        catch (HttpRequestException)
        {
            return LicenseValidationResult.Failure(
                LicenseErrorCode.NetworkError,
                "Unable to validate license - network error");
        }
    }

    private async Task<LicenseValidationResult> ValidateStoredLicenseAsync(StoredLicense storedLicense)
    {
        // First, try offline validation using the stored token
        var offlineContext = ParseLicenseToken(storedLicense.Token);

        // Check if license has expired
        if (offlineContext.ExpiresAt.HasValue && offlineContext.ExpiresAt.Value < DateTimeOffset.UtcNow)
        {
            return LicenseValidationResult.Failure(
                LicenseErrorCode.Expired,
                "License has expired");
        }

        // Try online validation
        try
        {
            var request = new LicenseValidationRequest
            {
                Token = storedLicense.Token,
                MachineId = storedLicense.MachineId
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{LicenseServerBaseUrl}/validate",
                request);

            if (response.IsSuccessStatusCode)
            {
                var validationResponse = await response.Content.ReadFromJsonAsync<LicenseValidationResponse>();
                if (validationResponse != null && validationResponse.Valid)
                {
                    // Update stored license with new validation time and potentially refreshed token
                    storedLicense.LastValidatedAt = DateTimeOffset.UtcNow;
                    if (!string.IsNullOrEmpty(validationResponse.Token))
                    {
                        storedLicense.Token = validationResponse.Token;
                    }
                    SaveStoredLicense(storedLicense);

                    var context = ParseLicenseToken(storedLicense.Token);
                    return LicenseValidationResult.Success(context);
                }
            }
        }
        catch (HttpRequestException)
        {
            // Network error - fall back to offline grace period
        }

        // Offline validation with grace period
        var daysSinceValidation = (DateTimeOffset.UtcNow - storedLicense.LastValidatedAt).TotalDays;
        if (daysSinceValidation <= GracePeriodDays)
        {
            var daysRemaining = (int)(GracePeriodDays - daysSinceValidation);
            return LicenseValidationResult.Success(
                offlineContext,
                isInGracePeriod: true,
                graceDaysRemaining: daysRemaining);
        }

        return LicenseValidationResult.Failure(
            LicenseErrorCode.GracePeriodExpired,
            "Offline grace period has expired. Please connect to the internet to validate your license.");
    }

    private ILicenseContext ParseLicenseToken(string token)
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

    private StoredLicense? LoadStoredLicense()
    {
        if (!File.Exists(LicenseFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(LicenseFilePath);
            return JsonSerializer.Deserialize<StoredLicense>(json);
        }
        catch
        {
            return null;
        }
    }

    private void SaveStoredLicense(StoredLicense license)
    {
        Directory.CreateDirectory(_dataDirectory);
        var json = JsonSerializer.Serialize(license, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(LicenseFilePath, json);
    }

    private static string GetMachineId()
    {
        // Generate a stable machine ID based on available hardware identifiers
        var identifier = Environment.MachineName + Environment.UserName;

        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(identifier));
        return Convert.ToHexString(hash)[..32];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
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
/// Stored license data.
/// </summary>
internal sealed class StoredLicense
{
    [JsonPropertyName("token")]
    public required string Token { get; set; }

    [JsonPropertyName("licenseKey")]
    public required string LicenseKey { get; set; }

    [JsonPropertyName("activatedAt")]
    public DateTimeOffset ActivatedAt { get; set; }

    [JsonPropertyName("lastValidatedAt")]
    public DateTimeOffset LastValidatedAt { get; set; }

    [JsonPropertyName("machineId")]
    public required string MachineId { get; set; }
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

// Request/Response DTOs

internal sealed class LicenseActivationRequest
{
    [JsonPropertyName("licenseKey")]
    public required string LicenseKey { get; set; }

    [JsonPropertyName("machineId")]
    public required string MachineId { get; set; }
}

internal sealed class LicenseActivationResponse
{
    [JsonPropertyName("token")]
    public required string Token { get; set; }
}

internal sealed class LicenseValidationRequest
{
    [JsonPropertyName("licenseKey")]
    public string? LicenseKey { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("machineId")]
    public required string MachineId { get; set; }
}

internal sealed class LicenseValidationResponse
{
    [JsonPropertyName("valid")]
    public bool Valid { get; set; }

    [JsonPropertyName("token")]
    public string? Token { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

internal sealed class LicenseDeactivationRequest
{
    [JsonPropertyName("token")]
    public required string Token { get; set; }

    [JsonPropertyName("machineId")]
    public required string MachineId { get; set; }
}

/// <summary>
/// Result of license activation.
/// </summary>
public sealed class LicenseActivationResult
{
    public bool Success { get; init; }
    public ILicenseContext? License { get; init; }
    public string? Message { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of license deactivation.
/// </summary>
public sealed class LicenseDeactivationResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Current license status.
/// </summary>
public sealed class LicenseStatus
{
    public required ILicenseContext License { get; init; }
    public bool IsFromEnvironment { get; init; }
    public DateTimeOffset? ActivatedAt { get; init; }
    public DateTimeOffset? LastValidatedAt { get; init; }
}
