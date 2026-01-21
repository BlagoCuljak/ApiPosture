using ApiPosture.Core.Discovery;

namespace ApiPosture.Core.Extensions;

/// <summary>
/// Interface for extensions that provide additional endpoint discoverers.
/// Used for features like secrets scanning that need custom analysis.
/// </summary>
public interface IDiscovererProvider
{
    /// <summary>
    /// Gets the endpoint discoverers provided by this extension.
    /// </summary>
    IReadOnlyList<IEndpointDiscoverer> GetDiscoverers();
}

/// <summary>
/// Interface for extensions that provide source analyzers.
/// Unlike discoverers, analyzers examine source code for patterns
/// without necessarily producing endpoints (e.g., secrets detection).
/// </summary>
public interface ISourceAnalyzerProvider
{
    /// <summary>
    /// Gets the source analyzers provided by this extension.
    /// </summary>
    IReadOnlyList<ISourceAnalyzer> GetAnalyzers();
}

/// <summary>
/// Interface for analyzing source code for security issues.
/// </summary>
public interface ISourceAnalyzer
{
    /// <summary>
    /// Unique identifier for this analyzer.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name for this analyzer.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Analyzes a source file and returns any findings.
    /// </summary>
    /// <param name="filePath">Path to the source file.</param>
    /// <param name="content">Content of the source file.</param>
    /// <returns>Any findings from the analysis.</returns>
    IEnumerable<Models.Finding> Analyze(string filePath, string content);
}
