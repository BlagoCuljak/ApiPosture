using System.Diagnostics;
using ApiPosture.Core.Discovery;
using ApiPosture.Core.Models;

namespace ApiPosture.Core.Analysis;

/// <summary>
/// Orchestrates the analysis of a project for API endpoints.
/// </summary>
public sealed class ProjectAnalyzer
{
    private readonly SourceFileLoader _fileLoader;
    private readonly IReadOnlyList<IEndpointDiscoverer> _discoverers;

    public ProjectAnalyzer() : this(
        new SourceFileLoader(),
        new IEndpointDiscoverer[]
        {
            new ControllerEndpointDiscoverer(),
            new MinimalApiEndpointDiscoverer()
        })
    {
    }

    public ProjectAnalyzer(
        SourceFileLoader fileLoader,
        IReadOnlyList<IEndpointDiscoverer> discoverers)
    {
        _fileLoader = fileLoader;
        _discoverers = discoverers;
    }

    /// <summary>
    /// Analyzes all C# files in the given path for API endpoints.
    /// </summary>
    public AnalysisResult Analyze(string path)
    {
        var stopwatch = Stopwatch.StartNew();
        var endpoints = new List<Endpoint>();
        var scannedFiles = new List<string>();

        foreach (var syntaxTree in _fileLoader.LoadAll(path))
        {
            scannedFiles.Add(syntaxTree.FilePath);

            foreach (var discoverer in _discoverers)
            {
                foreach (var endpoint in discoverer.Discover(syntaxTree))
                {
                    endpoints.Add(endpoint);
                }
            }
        }

        stopwatch.Stop();

        return new AnalysisResult(
            Endpoints: endpoints,
            ScannedFiles: scannedFiles,
            FailedFiles: _fileLoader.FailedFiles.ToList(),
            Duration: stopwatch.Elapsed);
    }
}

/// <summary>
/// Result of analyzing a project.
/// </summary>
public sealed record AnalysisResult(
    IReadOnlyList<Endpoint> Endpoints,
    IReadOnlyList<string> ScannedFiles,
    IReadOnlyList<string> FailedFiles,
    TimeSpan Duration);
