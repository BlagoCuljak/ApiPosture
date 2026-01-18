using ApiPosture.Core.Models;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Rules.Exposure;

/// <summary>
/// AP002: Detects [AllowAnonymous] on write operations (POST, PUT, DELETE).
/// </summary>
public sealed class AllowAnonymousOnWriteRule : ISecurityRule
{
    public string RuleId => "AP002";
    public string Name => "AllowAnonymous on write operation";
    public Severity DefaultSeverity => Severity.High;

    private static readonly HttpMethod WriteMethods =
        HttpMethod.Post | HttpMethod.Put | HttpMethod.Delete | HttpMethod.Patch;

    public Finding? Evaluate(Endpoint endpoint)
    {
        // Only check endpoints with explicit AllowAnonymous
        if (!endpoint.Authorization.HasAllowAnonymous)
            return null;

        // Check if any write methods are allowed
        if ((endpoint.Methods & WriteMethods) == HttpMethod.None)
            return null;

        var methods = GetWriteMethodsDisplay(endpoint.Methods);

        return new Finding
        {
            RuleId = RuleId,
            RuleName = Name,
            Severity = DefaultSeverity,
            Endpoint = endpoint,
            Message = $"Endpoint '{endpoint.Route}' allows anonymous access to write operations ({methods}).",
            Recommendation = "Reconsider allowing anonymous access to write operations. " +
                           "If intentional, ensure proper rate limiting, validation, and CSRF protection."
        };
    }

    private static string GetWriteMethodsDisplay(HttpMethod methods)
    {
        var writeMethods = new List<string>();

        if (methods.HasFlag(HttpMethod.Post)) writeMethods.Add("POST");
        if (methods.HasFlag(HttpMethod.Put)) writeMethods.Add("PUT");
        if (methods.HasFlag(HttpMethod.Delete)) writeMethods.Add("DELETE");
        if (methods.HasFlag(HttpMethod.Patch)) writeMethods.Add("PATCH");

        return string.Join(", ", writeMethods);
    }
}
