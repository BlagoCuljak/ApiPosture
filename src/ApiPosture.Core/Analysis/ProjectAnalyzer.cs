using System.Diagnostics;
using ApiPosture.Core.Authorization;
using ApiPosture.Core.Discovery;
using ApiPosture.Core.Models;
using Microsoft.CodeAnalysis;

namespace ApiPosture.Core.Analysis;

/// <summary>
/// Orchestrates the analysis of a project for API endpoints.
/// </summary>
public sealed class ProjectAnalyzer
{
    private readonly SourceFileLoader _fileLoader;
    private readonly IReadOnlyList<IEndpointDiscoverer> _discoverers;
    private readonly GlobalAuthorizationAnalyzer _globalAuthAnalyzer;
    private readonly RouteGroupRegistry _routeGroupRegistry;
    private readonly ExtensionMethodDiscoverer _extensionMethodDiscoverer;

    public ProjectAnalyzer() : this(
        new SourceFileLoader(),
        new IEndpointDiscoverer[]
        {
            new ControllerEndpointDiscoverer(),
            new MinimalApiEndpointDiscoverer()
        },
        new GlobalAuthorizationAnalyzer(),
        new RouteGroupRegistry(),
        new ExtensionMethodDiscoverer())
    {
    }

    public ProjectAnalyzer(
        SourceFileLoader fileLoader,
        IReadOnlyList<IEndpointDiscoverer> discoverers,
        GlobalAuthorizationAnalyzer? globalAuthAnalyzer = null,
        RouteGroupRegistry? routeGroupRegistry = null,
        ExtensionMethodDiscoverer? extensionMethodDiscoverer = null)
    {
        _fileLoader = fileLoader;
        _discoverers = discoverers;
        _globalAuthAnalyzer = globalAuthAnalyzer ?? new GlobalAuthorizationAnalyzer();
        _routeGroupRegistry = routeGroupRegistry ?? new RouteGroupRegistry();
        _extensionMethodDiscoverer = extensionMethodDiscoverer ?? new ExtensionMethodDiscoverer();
    }

    /// <summary>
    /// Analyzes all C# files in the given path for API endpoints.
    /// </summary>
    public AnalysisResult Analyze(string path)
    {
        var stopwatch = Stopwatch.StartNew();
        var endpoints = new List<Endpoint>();
        var scannedFiles = new List<string>();

        // Phase 1: Collect all syntax trees upfront
        var syntaxTrees = _fileLoader.LoadAll(path).ToList();
        foreach (var tree in syntaxTrees)
        {
            scannedFiles.Add(tree.FilePath);
        }

        // Phase 2: Analyze global authorization configuration
        var globalAuth = _globalAuthAnalyzer.Analyze(syntaxTrees);

        // Phase 3: Collect route groups and their extension method calls
        foreach (var tree in syntaxTrees)
        {
            _routeGroupRegistry.Analyze(tree);
        }

        // Phase 4: Discover extension methods
        var extensionMethods = new List<RouteExtensionMethod>();
        foreach (var tree in syntaxTrees)
        {
            extensionMethods.AddRange(_extensionMethodDiscoverer.DiscoverExtensionMethods(tree));
        }

        // Phase 5: Discover endpoints directly (standard discovery)
        var directEndpoints = new HashSet<string>(); // Track routes to avoid duplicates
        foreach (var syntaxTree in syntaxTrees)
        {
            foreach (var discoverer in _discoverers)
            {
                foreach (var endpoint in discoverer.Discover(syntaxTree, globalAuth))
                {
                    endpoints.Add(endpoint);
                    directEndpoints.Add(GetEndpointKey(endpoint));
                }
            }
        }

        // Phase 6: Correlate extension methods with route groups and discover additional endpoints
        foreach (var extensionMethod in extensionMethods)
        {
            var callingGroups = _routeGroupRegistry.GetGroupsCallingMethod(extensionMethod.MethodName);
            foreach (var group in callingGroups)
            {
                foreach (var endpoint in _extensionMethodDiscoverer.DiscoverEndpointsInMethod(
                    extensionMethod,
                    group.RoutePrefix,
                    group.Authorization,
                    globalAuth))
                {
                    // Avoid duplicates - extension method endpoints may have been discovered directly
                    var key = GetEndpointKey(endpoint);
                    if (!directEndpoints.Contains(key))
                    {
                        endpoints.Add(endpoint);
                        directEndpoints.Add(key);
                    }
                }
            }
        }

        stopwatch.Stop();

        return new AnalysisResult(
            Endpoints: endpoints,
            ScannedFiles: scannedFiles,
            FailedFiles: _fileLoader.FailedFiles.ToList(),
            Duration: stopwatch.Elapsed,
            GlobalAuthorization: globalAuth);
    }

    private static string GetEndpointKey(Endpoint endpoint)
    {
        return $"{endpoint.Route}|{endpoint.Methods}|{endpoint.Location.FilePath}:{endpoint.Location.LineNumber}";
    }
}

/// <summary>
/// Result of analyzing a project.
/// </summary>
public sealed record AnalysisResult(
    IReadOnlyList<Endpoint> Endpoints,
    IReadOnlyList<string> ScannedFiles,
    IReadOnlyList<string> FailedFiles,
    TimeSpan Duration,
    GlobalAuthorizationInfo? GlobalAuthorization = null);
