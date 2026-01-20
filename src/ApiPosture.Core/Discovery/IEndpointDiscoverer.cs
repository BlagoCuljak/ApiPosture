using ApiPosture.Core.Authorization;
using ApiPosture.Core.Models;
using Microsoft.CodeAnalysis;

namespace ApiPosture.Core.Discovery;

/// <summary>
/// Interface for endpoint discovery implementations.
/// </summary>
public interface IEndpointDiscoverer
{
    /// <summary>
    /// Discovers endpoints from a syntax tree.
    /// </summary>
    IEnumerable<Endpoint> Discover(SyntaxTree syntaxTree);

    /// <summary>
    /// Discovers endpoints from a syntax tree with global authorization context.
    /// </summary>
    IEnumerable<Endpoint> Discover(SyntaxTree syntaxTree, GlobalAuthorizationInfo globalAuth)
        => Discover(syntaxTree);
}
