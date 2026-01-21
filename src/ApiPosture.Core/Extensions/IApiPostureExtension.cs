namespace ApiPosture.Core.Extensions;

/// <summary>
/// Base interface for ApiPosture extensions.
/// Extensions must implement this interface to be loaded by the extension system.
/// </summary>
public interface IApiPostureExtension
{
    /// <summary>
    /// Unique identifier for this extension.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name for this extension.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Version of this extension.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Description of what this extension provides.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Required features for this extension to be enabled.
    /// These are checked against the license context.
    /// </summary>
    IReadOnlyList<string> RequiredFeatures { get; }

    /// <summary>
    /// Initializes the extension.
    /// Called after the extension is loaded and license is validated.
    /// </summary>
    /// <param name="context">The extension initialization context.</param>
    void Initialize(IExtensionContext context);
}

/// <summary>
/// Context provided to extensions during initialization.
/// </summary>
public interface IExtensionContext
{
    /// <summary>
    /// The directory where the extension is loaded from.
    /// </summary>
    string ExtensionDirectory { get; }

    /// <summary>
    /// The ApiPosture data directory (~/.apiposture).
    /// </summary>
    string DataDirectory { get; }

    /// <summary>
    /// Gets the license context for checking feature availability.
    /// </summary>
    Licensing.ILicenseContext License { get; }
}
