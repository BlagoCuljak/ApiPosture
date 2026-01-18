namespace ApiPosture.Core.Models;

/// <summary>
/// Represents a discovered API endpoint.
/// </summary>
public sealed class Endpoint
{
    /// <summary>
    /// The route template for the endpoint.
    /// </summary>
    public required string Route { get; init; }

    /// <summary>
    /// HTTP methods this endpoint responds to.
    /// </summary>
    public required HttpMethod Methods { get; init; }

    /// <summary>
    /// The type of endpoint (Controller or MinimalApi).
    /// </summary>
    public required EndpointType Type { get; init; }

    /// <summary>
    /// Source code location where the endpoint is defined.
    /// </summary>
    public required SourceLocation Location { get; init; }

    /// <summary>
    /// Name of the controller (for controller endpoints).
    /// </summary>
    public string? ControllerName { get; init; }

    /// <summary>
    /// Name of the action method (for controller endpoints).
    /// </summary>
    public string? ActionName { get; init; }

    /// <summary>
    /// Authorization configuration for this endpoint.
    /// </summary>
    public required AuthorizationInfo Authorization { get; init; }

    /// <summary>
    /// Security classification based on authorization analysis.
    /// </summary>
    public required SecurityClassification Classification { get; init; }

    /// <summary>
    /// Gets a display name for this endpoint.
    /// </summary>
    public string DisplayName => Type == EndpointType.Controller
        ? $"{ControllerName}.{ActionName}"
        : Route;

    /// <summary>
    /// Gets a formatted string of HTTP methods.
    /// </summary>
    public string MethodsDisplay
    {
        get
        {
            if (Methods == HttpMethod.All || Methods == HttpMethod.None)
                return Methods.ToString();

            var methods = new List<string>();
            foreach (HttpMethod method in Enum.GetValues<HttpMethod>())
            {
                if (method != HttpMethod.None && method != HttpMethod.All && Methods.HasFlag(method))
                    methods.Add(method.ToString().ToUpperInvariant());
            }
            return string.Join(", ", methods);
        }
    }
}
