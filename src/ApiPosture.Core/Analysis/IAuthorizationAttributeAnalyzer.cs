namespace ApiPosture.Core.Analysis;

/// <summary>
/// Analyzes custom attributes to determine if they implement authorization.
/// </summary>
public interface IAuthorizationAttributeAnalyzer
{
    /// <summary>
    /// Analyzes an attribute to determine if it implements authorization.
    /// Uses semantic analysis of source code to check if the attribute implements
    /// IAuthorizationFilter, IAsyncAuthorizationFilter, or inherits from AuthorizeAttribute.
    /// </summary>
    /// <param name="attributeName">Attribute name (e.g., "ApiKey")</param>
    /// <param name="sourceFilePath">Path to the file containing the endpoint</param>
    /// <returns>True if attribute implements authorization</returns>
    bool IsAuthorizationAttribute(string attributeName, string sourceFilePath);
}
