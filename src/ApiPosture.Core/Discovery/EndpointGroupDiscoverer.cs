using ApiPosture.Core.Authorization;
using ApiPosture.Core.Classification;
using ApiPosture.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Core.Discovery;

/// <summary>
/// Discovers endpoints from the EndpointGroupBase pattern popularised by the Clean Architecture
/// template (jasontaylordev/CleanArchitecture).
///
/// Handles non-static classes that expose a <c>Map(RouteGroupBuilder)</c> or
/// <c>Map(IEndpointRouteBuilder)</c> method and register routes using the handler-first overloads
/// provided by project-local extension methods:
///
///   Standard Minimal API:    groupBuilder.MapGet("/route", Handler);
///   Handler-first (no route): groupBuilder.MapGet(Handler);          // routes at group root
///   Handler-first (route):   groupBuilder.MapGet(Handler, "{id}");   // route is second arg
///
/// The route prefix is derived from the class name (e.g. class <c>TodoLists</c> → <c>/TodoLists</c>).
/// Group-level authorization (e.g. <c>groupBuilder.RequireAuthorization();</c>) is inherited by
/// all endpoints in that group.
/// </summary>
public sealed class EndpointGroupDiscoverer : IEndpointDiscoverer
{
    private static readonly HashSet<string> RouteBuilderParamTypes = new(StringComparer.Ordinal)
    {
        "RouteGroupBuilder",
        "IEndpointRouteBuilder",
        "WebApplication",
    };

    private static readonly Dictionary<string, HttpMethod> MapMethodNames = new(StringComparer.Ordinal)
    {
        ["MapGet"] = HttpMethod.Get,
        ["MapPost"] = HttpMethod.Post,
        ["MapPut"] = HttpMethod.Put,
        ["MapDelete"] = HttpMethod.Delete,
        ["MapPatch"] = HttpMethod.Patch,
    };

    private readonly SecurityClassifier _classifier = new();

    /// <inheritdoc />
    public IEnumerable<Endpoint> Discover(SyntaxTree syntaxTree)
        => Discover(syntaxTree, GlobalAuthorizationInfo.Empty);

    /// <inheritdoc />
    public IEnumerable<Endpoint> Discover(SyntaxTree syntaxTree, GlobalAuthorizationInfo globalAuth)
    {
        var root = syntaxTree.GetCompilationUnitRoot();
        var filePath = syntaxTree.FilePath;

        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            // Skip abstract classes (base-class definitions like EndpointGroupBase itself)
            if (classDecl.Modifiers.Any(SyntaxKind.AbstractKeyword))
                continue;

            var mapMethod = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(IsEndpointGroupMapMethod);

            if (mapMethod == null)
                continue;

            var className = classDecl.Identifier.Text;
            var paramName = mapMethod.ParameterList.Parameters[0].Identifier.Text;
            var routePrefix = $"/{className}";

            // Collect group-level auth before iterating endpoints
            var groupAuth = ExtractGroupLevelAuth(mapMethod, paramName);

            foreach (var endpoint in DiscoverEndpointsInMethod(
                mapMethod, filePath, routePrefix, paramName, groupAuth, globalAuth))
            {
                yield return endpoint;
            }
        }
    }

    private static bool IsEndpointGroupMapMethod(MethodDeclarationSyntax method)
    {
        if (method.Identifier.Text != "Map")
            return false;

        if (method.ParameterList.Parameters.Count != 1)
            return false;

        var paramType = method.ParameterList.Parameters[0].Type?.ToString();
        return paramType != null && RouteBuilderParamTypes.Any(t => paramType.Contains(t));
    }

    private IEnumerable<Endpoint> DiscoverEndpointsInMethod(
        MethodDeclarationSyntax mapMethod,
        string filePath,
        string routePrefix,
        string paramName,
        AuthorizationInfo groupAuth,
        GlobalAuthorizationInfo globalAuth)
    {
        var body = (SyntaxNode?)mapMethod.Body ?? mapMethod.ExpressionBody;
        if (body == null)
            yield break;

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var endpoint = TryCreateEndpoint(
                invocation, filePath, routePrefix, paramName, groupAuth, globalAuth);

            if (endpoint != null)
                yield return endpoint;
        }
    }

    private Endpoint? TryCreateEndpoint(
        InvocationExpressionSyntax invocation,
        string filePath,
        string routePrefix,
        string paramName,
        AuthorizationInfo groupAuth,
        GlobalAuthorizationInfo globalAuth)
    {
        var methodName = GetMethodName(invocation);
        if (methodName == null || !MapMethodNames.TryGetValue(methodName, out var httpMethod))
            return null;

        if (!IsCalledOnParam(invocation, paramName))
            return null;

        var subRoute = GetRouteTemplate(invocation);
        var fullRoute = CombineRoutes(routePrefix, subRoute);

        var endpointAuth = AnalyzeAuthChain(invocation, groupAuth, globalAuth);
        var classification = _classifier.Classify(endpointAuth);

        var location = invocation.GetLocation();
        var lineSpan = location.GetLineSpan();

        return new Endpoint
        {
            Route = fullRoute.StartsWith('/') ? fullRoute : "/" + fullRoute,
            Methods = httpMethod,
            Type = EndpointType.MinimalApi,
            Location = new SourceLocation(
                filePath,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1),
            Authorization = endpointAuth,
            Classification = classification
        };
    }

    /// <summary>
    /// Resolves the route template from a Map* invocation, handling both argument orderings:
    /// <list type="bullet">
    ///   <item>Standard:      <c>MapGet("/route", handler)</c> — first arg is a string</item>
    ///   <item>Handler-first: <c>MapGet(handler, "/route")</c> — second arg is a string</item>
    ///   <item>No route:      <c>MapGet(handler)</c>           — defaults to empty (group root)</item>
    /// </list>
    /// </summary>
    private static string GetRouteTemplate(InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
            return string.Empty;

        var firstArg = invocation.ArgumentList.Arguments[0].Expression;

        // Standard: first arg is a string literal route
        if (firstArg is LiteralExpressionSyntax literal &&
            literal.IsKind(SyntaxKind.StringLiteralExpression))
            return literal.Token.ValueText;

        if (firstArg is InterpolatedStringExpressionSyntax interpolated)
            return interpolated.ToString();

        // Handler-first: delegate/method-reference as first arg — check second arg for route
        if (invocation.ArgumentList.Arguments.Count >= 2)
        {
            var secondArg = invocation.ArgumentList.Arguments[1].Expression;
            if (secondArg is LiteralExpressionSyntax lit2 &&
                lit2.IsKind(SyntaxKind.StringLiteralExpression))
                return lit2.Token.ValueText;
        }

        // Handler-first with no explicit route — endpoint lives at the group root
        return string.Empty;
    }

    private static bool IsCalledOnParam(InvocationExpressionSyntax invocation, string paramName)
    {
        var current = invocation.Expression;
        while (current != null)
        {
            if (current is MemberAccessExpressionSyntax memberAccess)
            {
                if (memberAccess.Expression is IdentifierNameSyntax id &&
                    id.Identifier.Text == paramName)
                    return true;

                current = memberAccess.Expression;
            }
            else if (current is InvocationExpressionSyntax inner)
            {
                current = inner.Expression;
            }
            else
            {
                break;
            }
        }
        return false;
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation)
        => invocation.Expression switch
        {
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            IdentifierNameSyntax id => id.Identifier.Text,
            _ => null
        };

    /// <summary>
    /// Extracts authorization applied at the group/builder level — standalone calls that affect
    /// all endpoints in the group, e.g. <c>groupBuilder.RequireAuthorization();</c>.
    /// </summary>
    private static AuthorizationInfo ExtractGroupLevelAuth(
        MethodDeclarationSyntax method, string paramName)
    {
        var body = (SyntaxNode?)method.Body ?? method.ExpressionBody;
        if (body == null)
            return AuthorizationInfo.Empty;

        var hasRequireAuth = false;
        var hasAllowAnonymous = false;

        foreach (var invocation in body.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                continue;

            // Group-level: called directly on the builder param (not chained after a Map* call)
            if (memberAccess.Expression is not IdentifierNameSyntax id ||
                id.Identifier.Text != paramName)
                continue;

            switch (memberAccess.Name.Identifier.Text)
            {
                case "RequireAuthorization":
                    hasRequireAuth = true;
                    break;
                case "AllowAnonymous":
                    hasAllowAnonymous = true;
                    break;
            }
        }

        return new AuthorizationInfo
        {
            HasAuthorize = hasRequireAuth,
            HasAllowAnonymous = hasAllowAnonymous,
            Roles = [],
            Policies = [],
        };
    }

    private AuthorizationInfo AnalyzeAuthChain(
        InvocationExpressionSyntax invocation,
        AuthorizationInfo groupAuth,
        GlobalAuthorizationInfo globalAuth)
    {
        var hasRequireAuth = false;
        var hasAllowAnonymous = false;

        // Walk up the fluent chain to detect per-endpoint auth
        var current = invocation.Parent;
        while (current != null)
        {
            if (current is InvocationExpressionSyntax parentInvocation)
            {
                var name = parentInvocation.Expression switch
                {
                    MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
                    _ => null
                };

                switch (name)
                {
                    case "RequireAuthorization":
                        hasRequireAuth = true;
                        break;
                    case "AllowAnonymous":
                        hasAllowAnonymous = true;
                        break;
                }
            }

            if (current is MemberAccessExpressionSyntax)
                current = current.Parent;
            else if (current is InvocationExpressionSyntax)
                current = current.Parent;
            else
                break;
        }

        // Inherit group-level auth when the endpoint has no explicit override
        AuthorizationInfo? inheritedFrom =
            groupAuth.IsEffectivelyAuthorized || groupAuth.HasAllowAnonymous ? groupAuth : null;

        // Apply global fallback policy if nothing else is set
        if (!hasRequireAuth && !hasAllowAnonymous &&
            (inheritedFrom == null || !inheritedFrom.IsEffectivelyAuthorized) &&
            globalAuth.ProtectsAllEndpointsByDefault)
        {
            inheritedFrom = globalAuth.ToAuthorizationInfo();
        }

        return new AuthorizationInfo
        {
            HasAuthorize = hasRequireAuth,
            HasAllowAnonymous = hasAllowAnonymous,
            Roles = [],
            Policies = [],
            InheritedFrom = inheritedFrom
        };
    }

    private static string CombineRoutes(string prefix, string sub)
    {
        prefix = prefix.TrimEnd('/');
        sub = sub.TrimStart('/');
        return string.IsNullOrEmpty(sub) ? prefix : $"{prefix}/{sub}";
    }
}
