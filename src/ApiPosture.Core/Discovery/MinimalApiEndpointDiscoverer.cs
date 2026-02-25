using ApiPosture.Core.Authorization;
using ApiPosture.Core.Classification;
using ApiPosture.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Core.Discovery;

/// <summary>
/// Discovers endpoints from Minimal API patterns (app.MapGet, app.MapPost, etc.).
/// </summary>
public sealed class MinimalApiEndpointDiscoverer : IEndpointDiscoverer
{
    private static readonly Dictionary<string, HttpMethod> MapMethodNames = new(StringComparer.Ordinal)
    {
        ["MapGet"] = HttpMethod.Get,
        ["MapPost"] = HttpMethod.Post,
        ["MapPut"] = HttpMethod.Put,
        ["MapDelete"] = HttpMethod.Delete,
        ["MapPatch"] = HttpMethod.Patch,
        ["MapMethods"] = HttpMethod.All
    };

    private readonly SecurityClassifier _classifier = new();

    public IEnumerable<Endpoint> Discover(SyntaxTree syntaxTree)
        => Discover(syntaxTree, GlobalAuthorizationInfo.Empty);

    public IEnumerable<Endpoint> Discover(SyntaxTree syntaxTree, GlobalAuthorizationInfo globalAuth)
    {
        var root = syntaxTree.GetCompilationUnitRoot();
        var filePath = syntaxTree.FilePath;

        // Find all invocation expressions that match Map* patterns
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var endpoint = TryCreateEndpoint(invocation, filePath, globalAuth);
            if (endpoint != null)
                yield return endpoint;
        }
    }

    private Endpoint? TryCreateEndpoint(InvocationExpressionSyntax invocation, string filePath, GlobalAuthorizationInfo globalAuth)
    {
        // Check if this is a Map* method call
        var (methodName, httpMethod) = GetMapMethod(invocation);
        if (methodName == null || httpMethod == HttpMethod.None)
            return null;

        // Get the route template (first argument)
        var route = GetRouteTemplate(invocation);
        if (route == null)
            return null;

        // Check for group route prefix
        var groupRoute = GetGroupRoutePrefix(invocation);
        if (!string.IsNullOrEmpty(groupRoute))
        {
            route = CombineRoutes(groupRoute, route);
        }

        // Analyze the fluent chain for authorization
        var auth = AnalyzeAuthorizationChain(invocation, globalAuth);

        // Also check for [Authorize] attributes on lambda delegates (C# 10+ pattern used by eShopOnWeb)
        if (!auth.IsEffectivelyAuthorized && !auth.HasAllowAnonymous)
        {
            var lambdaAuth = CheckLambdaAttributes(invocation);
            if (lambdaAuth != null)
                auth = lambdaAuth;
        }

        var classification = _classifier.Classify(auth);

        var location = invocation.GetLocation();
        var lineSpan = location.GetLineSpan();

        return new Endpoint
        {
            Route = route.StartsWith('/') ? route : "/" + route,
            Methods = httpMethod,
            Type = EndpointType.MinimalApi,
            Location = new SourceLocation(
                filePath,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1),
            Authorization = auth,
            Classification = classification
        };
    }

    private static (string? MethodName, HttpMethod Method) GetMapMethod(InvocationExpressionSyntax invocation)
    {
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };

        if (methodName != null && MapMethodNames.TryGetValue(methodName, out var httpMethod))
        {
            return (methodName, httpMethod);
        }

        return (null, HttpMethod.None);
    }

    private static string? GetRouteTemplate(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
            return null;

        var firstArg = invocation.ArgumentList.Arguments[0].Expression;

        return firstArg switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) =>
                literal.Token.ValueText,
            InterpolatedStringExpressionSyntax interpolated => interpolated.ToString(),
            _ => null
        };
    }

    private static string? GetGroupRoutePrefix(InvocationExpressionSyntax invocation)
    {
        // Walk up the expression tree to find MapGroup
        var current = invocation.Expression;

        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is InvocationExpressionSyntax parentInvocation)
            {
                var parentMethod = parentInvocation.Expression switch
                {
                    MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                    _ => null
                };

                if (parentMethod == "MapGroup" && parentInvocation.ArgumentList.Arguments.Count > 0)
                {
                    var firstArg = parentInvocation.ArgumentList.Arguments[0].Expression;
                    if (firstArg is LiteralExpressionSyntax literal)
                    {
                        return literal.Token.ValueText;
                    }
                }

                current = parentInvocation.Expression;
            }
            else
            {
                current = memberAccess.Expression;
            }
        }

        return null;
    }

    private static string CombineRoutes(string prefix, string route)
    {
        prefix = prefix.TrimEnd('/');
        route = route.TrimStart('/');
        return $"{prefix}/{route}";
    }

    private AuthorizationInfo AnalyzeAuthorizationChain(InvocationExpressionSyntax invocation, GlobalAuthorizationInfo globalAuth)
    {
        var hasRequireAuth = false;
        var hasAllowAnonymous = false;
        var roles = new List<string>();
        var policies = new List<string>();

        // Walk up the fluent chain looking for auth methods
        var current = invocation.Parent;
        while (current != null)
        {
            if (current is InvocationExpressionSyntax parentInvocation)
            {
                var methodName = parentInvocation.Expression switch
                {
                    MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                    _ => null
                };

                switch (methodName)
                {
                    case "RequireAuthorization":
                        hasRequireAuth = true;
                        ExtractAuthorizationArgs(parentInvocation, policies, roles);
                        break;

                    case "AllowAnonymous":
                        hasAllowAnonymous = true;
                        break;

                    case "RequireRole":
                        hasRequireAuth = true;
                        ExtractRolesFromCall(parentInvocation, roles);
                        break;
                }
            }

            // Check if parent is member access (fluent chaining)
            if (current is MemberAccessExpressionSyntax)
            {
                current = current.Parent;
            }
            else if (current is InvocationExpressionSyntax)
            {
                current = current.Parent;
            }
            else
            {
                break;
            }
        }

        // Also check if the chain starts from a group with authorization
        var groupAuth = AnalyzeGroupAuthorization(invocation);

        // Determine inherited auth - prefer group auth, then fall back to global if needed
        AuthorizationInfo? inheritedFrom = groupAuth;

        // If no explicit auth on endpoint or group, and no AllowAnonymous, inherit from global FallbackPolicy
        if (!hasRequireAuth && !hasAllowAnonymous &&
            (groupAuth == null || !groupAuth.IsEffectivelyAuthorized) &&
            globalAuth.ProtectsAllEndpointsByDefault)
        {
            inheritedFrom = groupAuth != null
                ? new AuthorizationInfo
                {
                    HasAuthorize = groupAuth.HasAuthorize,
                    HasAllowAnonymous = groupAuth.HasAllowAnonymous,
                    Roles = groupAuth.Roles,
                    Policies = groupAuth.Policies,
                    AuthenticationSchemes = groupAuth.AuthenticationSchemes,
                    InheritedFrom = globalAuth.ToAuthorizationInfo()
                }
                : globalAuth.ToAuthorizationInfo();
        }

        return new AuthorizationInfo
        {
            HasAuthorize = hasRequireAuth,
            HasAllowAnonymous = hasAllowAnonymous,
            Roles = roles,
            Policies = policies,
            InheritedFrom = inheritedFrom
        };
    }

    private static void ExtractAuthorizationArgs(
        InvocationExpressionSyntax invocation,
        List<string> policies,
        List<string> roles)
    {
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                // String argument to RequireAuthorization is typically a policy
                policies.Add(literal.Token.ValueText);
            }
        }
    }

    private static void ExtractRolesFromCall(InvocationExpressionSyntax invocation, List<string> roles)
    {
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                roles.Add(literal.Token.ValueText);
            }
        }
    }

    private AuthorizationInfo? AnalyzeGroupAuthorization(InvocationExpressionSyntax invocation)
    {
        // Walk up to find MapGroup and analyze its authorization chain
        var current = invocation.Expression;

        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Expression is InvocationExpressionSyntax parentInvocation)
            {
                var parentMethod = parentInvocation.Expression switch
                {
                    MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                    _ => null
                };

                if (parentMethod == "MapGroup")
                {
                    // Analyze authorization on the group (don't inherit global auth here -
                    // that's handled at the endpoint level)
                    return AnalyzeGroupAuthorizationChain(parentInvocation);
                }

                current = parentInvocation.Expression;
            }
            else
            {
                current = memberAccess.Expression;
            }
        }

        return null;
    }

    private static AuthorizationInfo AnalyzeGroupAuthorizationChain(InvocationExpressionSyntax invocation)
    {
        var hasRequireAuth = false;
        var hasAllowAnonymous = false;
        var roles = new List<string>();
        var policies = new List<string>();

        // Walk up the fluent chain looking for auth methods
        var current = invocation.Parent;
        while (current != null)
        {
            if (current is InvocationExpressionSyntax parentInvocation)
            {
                var methodName = parentInvocation.Expression switch
                {
                    MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                    _ => null
                };

                switch (methodName)
                {
                    case "RequireAuthorization":
                        hasRequireAuth = true;
                        ExtractAuthorizationArgs(parentInvocation, policies, roles);
                        break;

                    case "AllowAnonymous":
                        hasAllowAnonymous = true;
                        break;

                    case "RequireRole":
                        hasRequireAuth = true;
                        ExtractRolesFromCall(parentInvocation, roles);
                        break;
                }
            }

            if (current is MemberAccessExpressionSyntax)
            {
                current = current.Parent;
            }
            else if (current is InvocationExpressionSyntax)
            {
                current = current.Parent;
            }
            else
            {
                break;
            }
        }

        return new AuthorizationInfo
        {
            HasAuthorize = hasRequireAuth,
            HasAllowAnonymous = hasAllowAnonymous,
            Roles = roles,
            Policies = policies
        };
    }

    /// <summary>
    /// Checks if any lambda argument to a Map* invocation has an [Authorize] attribute.
    /// Handles the C# 10+ pattern: app.MapDelete("route", [Authorize] async (args) => { })
    /// </summary>
    private static AuthorizationInfo? CheckLambdaAttributes(InvocationExpressionSyntax invocation)
    {
        var hasAuthorize = false;
        var hasAllowAnonymous = false;
        var roles = new List<string>();
        var policies = new List<string>();

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            // Lambda expressions can carry attribute lists in C# 10+
            // Only ParenthesizedLambdaExpressionSyntax supports AttributeLists;
            // AnonymousMethodExpressionSyntax does not (C# delegate syntax)
            SyntaxList<AttributeListSyntax> attrLists = default;

            if (argument.Expression is ParenthesizedLambdaExpressionSyntax lambda)
                attrLists = lambda.AttributeLists;

            foreach (var attrList in attrLists)
            {
                foreach (var attr in attrList.Attributes)
                {
                    var name = attr.Name.ToString();
                    if (name.Equals("Authorize", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".Authorize", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAuthorize = true;

                        // Extract roles and policies from attribute arguments
                        if (attr.ArgumentList != null)
                        {
                            foreach (var arg in attr.ArgumentList.Arguments)
                            {
                                var argName = arg.NameEquals?.Name.Identifier.Text;
                                var argValue = arg.Expression is LiteralExpressionSyntax lit
                                    ? lit.Token.ValueText
                                    : arg.Expression.ToString();

                                if (argName == "Roles")
                                    roles.Add(argValue);
                                else if (argName == "Policy")
                                    policies.Add(argValue);
                            }
                        }
                    }
                    else if (name.Equals("AllowAnonymous", StringComparison.OrdinalIgnoreCase) ||
                             name.EndsWith(".AllowAnonymous", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAllowAnonymous = true;
                    }
                }
            }
        }

        if (!hasAuthorize && !hasAllowAnonymous)
            return null;

        return new AuthorizationInfo
        {
            HasAuthorize = hasAuthorize,
            HasAllowAnonymous = hasAllowAnonymous,
            Roles = roles,
            Policies = policies
        };
    }
}
