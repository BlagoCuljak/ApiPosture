namespace ApiPosture.Core.Models;

/// <summary>
/// Type of endpoint discovery source.
/// </summary>
public enum EndpointType
{
    /// <summary>
    /// Traditional MVC/API controller endpoint.
    /// </summary>
    Controller,

    /// <summary>
    /// Minimal API endpoint (MapGet, MapPost, etc.).
    /// </summary>
    MinimalApi
}
