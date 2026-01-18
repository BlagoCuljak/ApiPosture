using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiPosture.Core.Configuration;

/// <summary>
/// Loads and parses ApiPosture configuration files.
/// </summary>
public sealed class ConfigurationLoader
{
    private const string ConfigFileName = ".apiposture.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Loads configuration from the specified path, or discovers it from conventional locations.
    /// </summary>
    /// <param name="explicitPath">Explicit config file path (highest priority).</param>
    /// <param name="targetDirectory">Directory being scanned (second priority).</param>
    /// <param name="currentDirectory">Current working directory (third priority).</param>
    /// <returns>Loaded configuration or empty configuration if not found.</returns>
    public ApiPostureConfig Load(string? explicitPath, string? targetDirectory, string? currentDirectory)
    {
        var configPath = ResolveConfigPath(explicitPath, targetDirectory, currentDirectory);

        if (configPath is null)
            return ApiPostureConfig.Empty;

        return LoadFromFile(configPath);
    }

    /// <summary>
    /// Loads configuration from a specific file path.
    /// </summary>
    public ApiPostureConfig LoadFromFile(string path)
    {
        if (!File.Exists(path))
            return ApiPostureConfig.Empty;

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<ApiPostureConfig>(json, JsonOptions);
            return config ?? ApiPostureConfig.Empty;
        }
        catch (JsonException)
        {
            // Invalid JSON - return empty config
            // In a production scenario, we might want to log this or throw
            return ApiPostureConfig.Empty;
        }
    }

    /// <summary>
    /// Resolves the configuration file path based on priority order.
    /// </summary>
    /// <returns>The resolved path, or null if no config file found.</returns>
    public string? ResolveConfigPath(string? explicitPath, string? targetDirectory, string? currentDirectory)
    {
        // Priority 1: Explicit path
        if (!string.IsNullOrEmpty(explicitPath))
        {
            return File.Exists(explicitPath) ? explicitPath : null;
        }

        // Priority 2: Target directory
        if (!string.IsNullOrEmpty(targetDirectory))
        {
            var targetPath = Path.Combine(targetDirectory, ConfigFileName);
            if (File.Exists(targetPath))
                return targetPath;
        }

        // Priority 3: Current directory
        if (!string.IsNullOrEmpty(currentDirectory))
        {
            var currentPath = Path.Combine(currentDirectory, ConfigFileName);
            if (File.Exists(currentPath))
                return currentPath;
        }

        return null;
    }

    /// <summary>
    /// Checks if a configuration file exists at the specified location or conventional locations.
    /// </summary>
    public bool ConfigExists(string? explicitPath, string? targetDirectory, string? currentDirectory)
    {
        return ResolveConfigPath(explicitPath, targetDirectory, currentDirectory) is not null;
    }
}
