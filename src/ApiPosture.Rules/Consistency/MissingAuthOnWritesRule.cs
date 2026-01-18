using ApiPosture.Core.Models;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Rules.Consistency;

/// <summary>
/// AP004: Detects public POST/PUT/PATCH/DELETE endpoints without any authorization.
/// </summary>
public sealed class MissingAuthOnWritesRule : ISecurityRule
{
    public string RuleId => "AP004";
    public string Name => "Missing authorization on write operations";
    public Severity DefaultSeverity => Severity.Critical;

    private static readonly HttpMethod WriteMethods =
        HttpMethod.Post | HttpMethod.Put | HttpMethod.Delete | HttpMethod.Patch;

    public Finding? Evaluate(Endpoint endpoint)
    {
        // Only triggers for public endpoints without explicit AllowAnonymous
        if (endpoint.Classification != SecurityClassification.Public)
            return null;

        // If AllowAnonymous is explicit, rule AP002 handles it
        if (endpoint.Authorization.HasAllowAnonymous)
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
            Message = $"Endpoint '{endpoint.Route}' allows {methods} without any authorization.",
            Recommendation = "Add [Authorize] attribute to require authentication, or add [AllowAnonymous] " +
                           "to explicitly document that public write access is intentional."
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
