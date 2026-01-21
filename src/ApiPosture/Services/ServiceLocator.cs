using ApiPosture.Core.Extensions;
using ApiPosture.Core.Licensing;
using ApiPosture.Extensions;
using ApiPosture.Licensing;

namespace ApiPosture.Services;

/// <summary>
/// Simple service locator for sharing services across commands.
/// Spectre.Console.Cli doesn't support built-in DI, so we use this pattern.
/// </summary>
public static class ServiceLocator
{
    private static LicenseManager? _licenseManager;
    private static ExtensionLoader? _extensionLoader;
    private static ILicenseContext? _licenseContext;
    private static ExtensionLoadResult? _extensionLoadResult;
    private static bool _initialized;

    /// <summary>
    /// Gets the license manager.
    /// </summary>
    public static LicenseManager LicenseManager
    {
        get
        {
            EnsureInitialized();
            return _licenseManager!;
        }
    }

    /// <summary>
    /// Gets the extension loader.
    /// </summary>
    public static ExtensionLoader ExtensionLoader
    {
        get
        {
            EnsureInitialized();
            return _extensionLoader!;
        }
    }

    /// <summary>
    /// Gets the current license context.
    /// </summary>
    public static ILicenseContext LicenseContext
    {
        get
        {
            EnsureInitialized();
            return _licenseContext!;
        }
    }

    /// <summary>
    /// Gets the extension load result (includes errors).
    /// </summary>
    public static ExtensionLoadResult ExtensionLoadResult
    {
        get
        {
            EnsureInitialized();
            return _extensionLoadResult!;
        }
    }

    /// <summary>
    /// Gets all extension rules.
    /// </summary>
    public static IReadOnlyList<IExtensionRule> ExtensionRules
    {
        get
        {
            EnsureInitialized();
            return _extensionLoader!.GetAllRules();
        }
    }

    /// <summary>
    /// Gets all extension commands.
    /// </summary>
    public static IReadOnlyList<CommandDescriptor> ExtensionCommands
    {
        get
        {
            EnsureInitialized();
            return _extensionLoader!.GetAllCommands();
        }
    }

    /// <summary>
    /// Initializes the services.
    /// </summary>
    public static void Initialize()
    {
        if (_initialized) return;

        var dataDirectory = Extensions.ExtensionLoader.DataDirectory;

        // Initialize license manager
        _licenseManager = new LicenseManager(dataDirectory);
        _licenseContext = _licenseManager.GetLicenseContext();

        // Initialize extension loader
        var extensionsDirectory = Extensions.ExtensionLoader.DefaultExtensionsDirectory;
        _extensionLoader = new ExtensionLoader(extensionsDirectory, _licenseContext);
        _extensionLoadResult = _extensionLoader.LoadAll();

        _initialized = true;
    }

    /// <summary>
    /// Ensures services are initialized.
    /// </summary>
    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }

    /// <summary>
    /// Disposes all services.
    /// </summary>
    public static void Dispose()
    {
        _licenseManager?.Dispose();
        _extensionLoader?.Dispose();
        _initialized = false;
    }
}
