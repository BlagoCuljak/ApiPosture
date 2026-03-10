using ApiPosture.Core.Models;

namespace ApiPosture.Rules;

/// <summary>
/// Classifies anonymous write endpoints to determine context-appropriate severity.
/// Shared by AP002 (AllowAnonymousOnWriteRule) and AP004 (MissingAuthOnWritesRule).
/// </summary>
public static class AnonymousWriteClassifier
{
    // Auth action names that identify login/registration endpoints regardless of controller prefix.
    private static readonly HashSet<string> KnownAuthActions = new(StringComparer.OrdinalIgnoreCase)
    {
        "login", "signin", "register", "signup", "token",
        "refresh", "revoke", "authenticate", "logout", "signout"
    };

    /// <summary>
    /// Returns true for endpoint routes that are authentication endpoints by convention.
    /// These are intentionally public by design and do not benefit from AP001/AP004 findings.
    /// Used to suppress false positives on well-known public patterns.
    ///
    /// Matches:
    ///   - Well-known auth/identity/account controller prefixes (/auth/, /identity/, /account/)
    ///   - Terminal route segments that are recognised auth actions (login, register, signin, etc.)
    ///     regardless of the controller prefix, e.g. /api/identity/login or /api/users/login.
    /// </summary>
    public static bool IsKnownAuthEndpoint(Endpoint endpoint)
    {
        var route = endpoint.Route.ToLowerInvariant();

        // Well-known controller namespaces used for authentication
        if (route.Contains("/auth/") || route.Contains("/identity/") || route.Contains("/account/"))
            return true;

        // Match by terminal path segment so /login matches but /login-history does not
        var segments = route.Split('/');
        var lastSegment = segments.LastOrDefault(s => s.Length > 0 && !s.StartsWith('{')) ?? string.Empty;
        return KnownAuthActions.Contains(lastSegment);
    }

    /// <summary>
    /// Categorizes an anonymous write endpoint and returns appropriate severity and context message.
    /// </summary>
    public static (Severity Severity, string Context) CategorizeAnonymousWrite(Endpoint endpoint)
    {
        var route = endpoint.Route.ToLowerInvariant();

        // Webhook endpoints - must be public for external services (Stripe, PayPal, etc.)
        if (route.Contains("/webhook") || route.Contains("/hook") ||
            route.Contains("/callback") || route.Contains("/notify"))
            return (Severity.Medium, "Webhook endpoints require signature validation instead of authentication.");

        // Authentication endpoints - must be public by design
        if (IsKnownAuthEndpoint(endpoint))
            return (Severity.Low, "Authentication endpoints are intentionally public. Ensure rate limiting and account lockout are implemented.");

        // Counter/analytics endpoints - low risk, intentionally public
        if (route.Contains("increment") || route.Contains("/count") ||
            route.Contains("/view") || route.Contains("/track") || route.Contains("/analytics"))
            return (Severity.Low, "Consider adding rate limiting to prevent abuse.");

        // Token/device registration endpoints
        if (route.Contains("/register-token") || route.Contains("/register-device") ||
            route.Contains("/subscribe") || route.Contains("/unsubscribe"))
            return (Severity.Medium, "Consider adding rate limiting for registration endpoints.");

        // Default: high risk for AP002, critical for AP004
        return (Severity.High, "Ensure proper rate limiting, validation, and CSRF protection.");
    }

    /// <summary>
    /// Returns context-appropriate severity for AP004 (MissingAuthOnWritesRule).
    /// AP004 defaults to Critical instead of High for unrecognized patterns.
    /// </summary>
    public static (Severity Severity, string Context) CategorizeForMissingAuth(Endpoint endpoint)
    {
        var (severity, context) = CategorizeAnonymousWrite(endpoint);

        // For AP004, upgrade the default severity since these endpoints lack even explicit AllowAnonymous
        if (severity == Severity.High)
            return (Severity.Critical, context);

        // Webhooks: High instead of Medium (no explicit AllowAnonymous is worse)
        if (severity == Severity.Medium)
            return (Severity.High, context);

        // Counters stay at Medium instead of Low
        if (severity == Severity.Low)
            return (Severity.Medium, context);

        return (severity, context);
    }
}
