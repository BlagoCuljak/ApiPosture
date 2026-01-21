using System.Reflection;
using System.Runtime.Loader;
using ApiPosture.Core.Extensions;
using ApiPosture.Core.Licensing;

namespace ApiPosture.Extensions;

/// <summary>
/// Loads and manages ApiPosture extensions.
/// </summary>
public sealed class ExtensionLoader : IDisposable
{
    private readonly string _extensionsDirectory;
    private readonly ILicenseContext _licenseContext;
    private readonly List<ExtensionLoadContext> _loadContexts = new();
    private readonly List<LoadedExtension> _loadedExtensions = new();
    private bool _disposed;

    /// <summary>
    /// Creates a new extension loader.
    /// </summary>
    /// <param name="extensionsDirectory">Directory containing extension DLLs.</param>
    /// <param name="licenseContext">License context for feature checking.</param>
    public ExtensionLoader(string extensionsDirectory, ILicenseContext licenseContext)
    {
        _extensionsDirectory = extensionsDirectory;
        _licenseContext = licenseContext;
    }

    /// <summary>
    /// Gets the default extensions directory (~/.apiposture/extensions).
    /// </summary>
    public static string DefaultExtensionsDirectory
    {
        get
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".apiposture", "extensions");
        }
    }

    /// <summary>
    /// Gets the ApiPosture data directory (~/.apiposture).
    /// </summary>
    public static string DataDirectory
    {
        get
        {
            var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(homeDir, ".apiposture");
        }
    }

    /// <summary>
    /// Gets all successfully loaded extensions.
    /// </summary>
    public IReadOnlyList<LoadedExtension> Extensions => _loadedExtensions;

    /// <summary>
    /// Discovers and loads all extensions from the extensions directory.
    /// </summary>
    /// <returns>A result containing loaded extensions and any errors.</returns>
    public ExtensionLoadResult LoadAll()
    {
        var errors = new List<ExtensionLoadError>();

        if (!Directory.Exists(_extensionsDirectory))
        {
            return new ExtensionLoadResult
            {
                Extensions = Array.Empty<LoadedExtension>(),
                Errors = errors
            };
        }

        // Find all DLL files in the extensions directory
        var dllFiles = Directory.GetFiles(_extensionsDirectory, "*.dll", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith("ApiPosture.Core")) // Skip core assemblies
            .Where(f => !Path.GetFileName(f).StartsWith("ApiPosture.Rules"))
            .ToList();

        foreach (var dllPath in dllFiles)
        {
            try
            {
                var result = LoadExtension(dllPath);
                if (result.Extension != null)
                {
                    _loadedExtensions.Add(result.Extension);
                }
                if (result.Error != null)
                {
                    errors.Add(result.Error);
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ExtensionLoadError
                {
                    FilePath = dllPath,
                    Message = $"Failed to load extension: {ex.Message}",
                    Exception = ex
                });
            }
        }

        return new ExtensionLoadResult
        {
            Extensions = _loadedExtensions,
            Errors = errors
        };
    }

    private (LoadedExtension? Extension, ExtensionLoadError? Error) LoadExtension(string dllPath)
    {
        // Create isolated load context for this extension
        var loadContext = new ExtensionLoadContext(dllPath);
        _loadContexts.Add(loadContext);

        var assembly = loadContext.LoadFromAssemblyPath(dllPath);

        // Find types implementing IApiPostureExtension
        var extensionTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IApiPostureExtension).IsAssignableFrom(t))
            .ToList();

        if (extensionTypes.Count == 0)
        {
            return (null, new ExtensionLoadError
            {
                FilePath = dllPath,
                Message = "No IApiPostureExtension implementation found"
            });
        }

        // Load the first extension type found
        var extensionType = extensionTypes[0];
        var extension = (IApiPostureExtension)Activator.CreateInstance(extensionType)!;

        // Check if all required features are available
        if (!_licenseContext.HasAllFeatures(extension.RequiredFeatures))
        {
            var missingFeatures = extension.RequiredFeatures
                .Where(f => !_licenseContext.HasFeature(f))
                .ToList();

            return (null, new ExtensionLoadError
            {
                FilePath = dllPath,
                Message = $"Extension '{extension.Name}' requires features not available in your license: {string.Join(", ", missingFeatures)}",
                MissingFeatures = missingFeatures
            });
        }

        // Initialize the extension
        var context = new ExtensionContext(
            Path.GetDirectoryName(dllPath)!,
            DataDirectory,
            _licenseContext);
        extension.Initialize(context);

        // Extract providers
        var loaded = new LoadedExtension
        {
            Extension = extension,
            Assembly = assembly,
            FilePath = dllPath,
            RuleProvider = extension as IRuleProvider,
            CommandProvider = extension as ICommandProvider,
            DiscovererProvider = extension as IDiscovererProvider,
            SourceAnalyzerProvider = extension as ISourceAnalyzerProvider,
            AnalysisPhaseProvider = extension as IAnalysisPhaseProvider
        };

        return (loaded, null);
    }

    /// <summary>
    /// Gets all rules from loaded extensions.
    /// </summary>
    public IReadOnlyList<IExtensionRule> GetAllRules()
    {
        return _loadedExtensions
            .Where(e => e.RuleProvider != null)
            .SelectMany(e => e.RuleProvider!.GetRules())
            .ToList();
    }

    /// <summary>
    /// Gets all command descriptors from loaded extensions.
    /// </summary>
    public IReadOnlyList<CommandDescriptor> GetAllCommands()
    {
        return _loadedExtensions
            .Where(e => e.CommandProvider != null)
            .SelectMany(e => e.CommandProvider!.GetCommands())
            .ToList();
    }

    /// <summary>
    /// Gets all source analyzers from loaded extensions.
    /// </summary>
    public IReadOnlyList<ISourceAnalyzer> GetAllSourceAnalyzers()
    {
        return _loadedExtensions
            .Where(e => e.SourceAnalyzerProvider != null)
            .SelectMany(e => e.SourceAnalyzerProvider!.GetAnalyzers())
            .ToList();
    }

    /// <summary>
    /// Gets all analysis phases from loaded extensions, sorted by priority.
    /// </summary>
    public IReadOnlyList<IAnalysisPhase> GetAllAnalysisPhases()
    {
        return _loadedExtensions
            .Where(e => e.AnalysisPhaseProvider != null)
            .SelectMany(e => e.AnalysisPhaseProvider!.GetPhases())
            .OrderBy(p => p.Priority)
            .ToList();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var context in _loadContexts)
        {
            context.Unload();
        }
        _loadContexts.Clear();
        _loadedExtensions.Clear();
    }
}

/// <summary>
/// AssemblyLoadContext for loading extensions in isolation.
/// </summary>
internal sealed class ExtensionLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public ExtensionLoadContext(string extensionPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(extensionPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // First check if it's already loaded in the default context (core assemblies)
        var defaultAssembly = Default.Assemblies
            .FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
        if (defaultAssembly != null)
        {
            return defaultAssembly;
        }

        // Try to resolve from extension directory
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }
}

/// <summary>
/// Context implementation for extensions.
/// </summary>
internal sealed class ExtensionContext : IExtensionContext
{
    public ExtensionContext(string extensionDirectory, string dataDirectory, ILicenseContext license)
    {
        ExtensionDirectory = extensionDirectory;
        DataDirectory = dataDirectory;
        License = license;
    }

    public string ExtensionDirectory { get; }
    public string DataDirectory { get; }
    public ILicenseContext License { get; }
}

/// <summary>
/// Result of loading extensions.
/// </summary>
public sealed class ExtensionLoadResult
{
    /// <summary>
    /// Successfully loaded extensions.
    /// </summary>
    public required IReadOnlyList<LoadedExtension> Extensions { get; init; }

    /// <summary>
    /// Errors encountered during loading.
    /// </summary>
    public required IReadOnlyList<ExtensionLoadError> Errors { get; init; }
}

/// <summary>
/// A successfully loaded extension.
/// </summary>
public sealed class LoadedExtension
{
    /// <summary>
    /// The extension instance.
    /// </summary>
    public required IApiPostureExtension Extension { get; init; }

    /// <summary>
    /// The assembly containing the extension.
    /// </summary>
    public required Assembly Assembly { get; init; }

    /// <summary>
    /// Path to the extension DLL.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Rule provider if the extension provides rules.
    /// </summary>
    public IRuleProvider? RuleProvider { get; init; }

    /// <summary>
    /// Command provider if the extension provides commands.
    /// </summary>
    public ICommandProvider? CommandProvider { get; init; }

    /// <summary>
    /// Discoverer provider if the extension provides discoverers.
    /// </summary>
    public IDiscovererProvider? DiscovererProvider { get; init; }

    /// <summary>
    /// Source analyzer provider if the extension provides analyzers.
    /// </summary>
    public ISourceAnalyzerProvider? SourceAnalyzerProvider { get; init; }

    /// <summary>
    /// Analysis phase provider if the extension provides phases.
    /// </summary>
    public IAnalysisPhaseProvider? AnalysisPhaseProvider { get; init; }
}

/// <summary>
/// Error encountered while loading an extension.
/// </summary>
public sealed class ExtensionLoadError
{
    /// <summary>
    /// Path to the extension file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// The exception if one was thrown.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Missing license features if the extension requires them.
    /// </summary>
    public IReadOnlyList<string>? MissingFeatures { get; init; }
}
