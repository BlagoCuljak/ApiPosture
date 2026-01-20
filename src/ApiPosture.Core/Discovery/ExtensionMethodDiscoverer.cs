using ApiPosture.Core.Authorization;
using ApiPosture.Core.Classification;
using ApiPosture.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Core.Discovery;

/// <summary>
/// Represents an extension method that registers routes.
/// </summary>
public sealed class RouteExtensionMethod
{
    /// <summary>
    /// The name of the extension method (e.g., "MapTasksRoutes").
    /// </summary>
    public required string MethodName { get; init; }

    /// <summary>
    /// The type being extended (e.g., "IEndpointRouteBuilder", "RouteGroupBuilder").
    /// </summary>
    public string? ExtendedType { get; init; }

    /// <summary>
    /// The name of the parameter representing the route builder.
    /// </summary>
    public required string ParameterName { get; init; }

    /// <summary>
    /// The syntax node of the method body for endpoint discovery.
    /// </summary>
    public required SyntaxNode MethodBody { get; init; }

    /// <summary>
    /// The file where this extension method is defined.
    /// </summary>
    public required string FilePath { get; init; }
}

/// <summary>
/// Discovers extension methods that register routes on RouteGroupBuilder or IEndpointRouteBuilder.
/// </summary>
public sealed class ExtensionMethodDiscoverer
{
    private static readonly HashSet<string> RouteBuilderTypes = new(StringComparer.Ordinal)
    {
        "IEndpointRouteBuilder",
        "RouteGroupBuilder",
        "WebApplication",
        "IApplicationBuilder"
    };

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

    /// <summary>
    /// Discovers route extension methods from a syntax tree.
    /// </summary>
    public IEnumerable<RouteExtensionMethod> DiscoverExtensionMethods(SyntaxTree syntaxTree)
    {
        var root = syntaxTree.GetCompilationUnitRoot();
        var filePath = syntaxTree.FilePath;

        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            // Only look at static classes (extension method containers)
            if (!classDecl.Modifiers.Any(SyntaxKind.StaticKeyword))
                continue;

            foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
            {
                var extensionMethod = TryGetRouteExtensionMethod(method, filePath);
                if (extensionMethod != null)
                    yield return extensionMethod;
            }
        }
    }

    private RouteExtensionMethod? TryGetRouteExtensionMethod(MethodDeclarationSyntax method, string filePath)
    {
        // Must be static
        if (!method.Modifiers.Any(SyntaxKind.StaticKeyword))
            return null;

        // Must have parameters
        if (method.ParameterList.Parameters.Count == 0)
            return null;

        var firstParam = method.ParameterList.Parameters[0];

        // Must be an extension method (has 'this' modifier)
        if (!firstParam.Modifiers.Any(SyntaxKind.ThisKeyword))
            return null;

        // Check if the extended type looks like a route builder
        var extendedType = firstParam.Type?.ToString();
        if (extendedType == null || !IsRouteBuilderType(extendedType))
            return null;

        // Check if method name matches pattern (Map*Routes)
        var methodName = method.Identifier.Text;
        if (!methodName.StartsWith("Map", StringComparison.Ordinal) ||
            !methodName.EndsWith("Routes", StringComparison.Ordinal))
            return null;

        var body = method.Body as SyntaxNode ?? method.ExpressionBody;
        if (body == null)
            return null;

        return new RouteExtensionMethod
        {
            MethodName = methodName,
            ExtendedType = extendedType,
            ParameterName = firstParam.Identifier.Text,
            MethodBody = body,
            FilePath = filePath
        };
    }

    private static bool IsRouteBuilderType(string typeName)
    {
        foreach (var knownType in RouteBuilderTypes)
        {
            if (typeName.Contains(knownType, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Discovers endpoints within an extension method body, applying parent context.
    /// </summary>
    public IEnumerable<Endpoint> DiscoverEndpointsInMethod(
        RouteExtensionMethod extensionMethod,
        string parentRoutePrefix,
        AuthorizationInfo parentAuth,
        GlobalAuthorizationInfo globalAuth)
    {
        // Find all Map* invocations in the method body
        foreach (var invocation in extensionMethod.MethodBody.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var endpoint = TryCreateEndpoint(
                invocation,
                extensionMethod.FilePath,
                extensionMethod.ParameterName,
                parentRoutePrefix,
                parentAuth,
                globalAuth);

            if (endpoint != null)
                yield return endpoint;
        }
    }

    private Endpoint? TryCreateEndpoint(
        InvocationExpressionSyntax invocation,
        string filePath,
        string parameterName,
        string parentRoutePrefix,
        AuthorizationInfo parentAuth,
        GlobalAuthorizationInfo globalAuth)
    {
        var (methodName, httpMethod) = GetMapMethod(invocation);
        if (methodName == null || httpMethod == HttpMethod.None)
            return null;

        // Verify this is called on the parameter or a group derived from it
        if (!IsCalledOnRouteBuilder(invocation, parameterName))
            return null;

        var route = GetRouteTemplate(invocation);
        if (route == null)
            return null;

        // Check for nested MapGroup within the extension method
        var nestedGroupRoute = GetNestedGroupRoutePrefix(invocation);
        if (!string.IsNullOrEmpty(nestedGroupRoute))
        {
            parentRoutePrefix = CombineRoutes(parentRoutePrefix, nestedGroupRoute);
        }

        // Combine with parent route prefix
        route = CombineRoutes(parentRoutePrefix, route);

        // Analyze the fluent chain for authorization
        var auth = AnalyzeAuthorizationChain(invocation, parentAuth, globalAuth);
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

    private static bool IsCalledOnRouteBuilder(InvocationExpressionSyntax invocation, string parameterName)
    {
        // Check if this is called on the parameter directly or through a chain
        var current = invocation.Expression;
        while (current != null)
        {
            switch (current)
            {
                case MemberAccessExpressionSyntax memberAccess:
                    if (memberAccess.Expression is IdentifierNameSyntax identifier &&
                        identifier.Identifier.Text == parameterName)
                    {
                        return true;
                    }
                    current = memberAccess.Expression;
                    break;

                case IdentifierNameSyntax id when id.Identifier.Text == parameterName:
                    return true;

                default:
                    // Check if it's part of a fluent chain from MapGroup
                    if (current is InvocationExpressionSyntax nestedInvocation)
                    {
                        var nestedMethod = nestedInvocation.Expression switch
                        {
                            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                            _ => null
                        };

                        if (nestedMethod == "MapGroup")
                        {
                            return true;
                        }

                        current = nestedInvocation.Expression switch
                        {
                            MemberAccessExpressionSyntax ma => ma.Expression,
                            _ => null
                        };
                    }
                    else
                    {
                        return false;
                    }
                    break;
            }
        }

        return false;
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

    private static string? GetNestedGroupRoutePrefix(InvocationExpressionSyntax invocation)
    {
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
        if (string.IsNullOrEmpty(prefix))
            return route;
        if (string.IsNullOrEmpty(route))
            return prefix;

        prefix = prefix.TrimEnd('/');
        route = route.TrimStart('/');
        return $"{prefix}/{route}";
    }

    private AuthorizationInfo AnalyzeAuthorizationChain(
        InvocationExpressionSyntax invocation,
        AuthorizationInfo parentAuth,
        GlobalAuthorizationInfo globalAuth)
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
                        ExtractAuthorizationArgs(parentInvocation, policies);
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

        // Determine inherited auth
        AuthorizationInfo? inheritedFrom = parentAuth;

        // If no explicit auth and no AllowAnonymous, check global fallback
        if (!hasRequireAuth && !hasAllowAnonymous && !parentAuth.IsEffectivelyAuthorized &&
            globalAuth.ProtectsAllEndpointsByDefault)
        {
            inheritedFrom = new AuthorizationInfo
            {
                HasAuthorize = parentAuth.HasAuthorize,
                HasAllowAnonymous = parentAuth.HasAllowAnonymous,
                Roles = parentAuth.Roles,
                Policies = parentAuth.Policies,
                AuthenticationSchemes = parentAuth.AuthenticationSchemes,
                InheritedFrom = globalAuth.ToAuthorizationInfo()
            };
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

    private static void ExtractAuthorizationArgs(InvocationExpressionSyntax invocation, List<string> policies)
    {
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
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
}
