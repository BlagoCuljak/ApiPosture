using ApiPosture.Core.Authorization;
using ApiPosture.Core.Classification;
using ApiPosture.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using HttpMethod = ApiPosture.Core.Models.HttpMethod;

namespace ApiPosture.Core.Discovery;

/// <summary>
/// Discovers endpoints from ASP.NET Core controller classes.
/// </summary>
public sealed class ControllerEndpointDiscoverer : IEndpointDiscoverer
{
    private static readonly HashSet<string> ControllerAttributeNames = new(StringComparer.Ordinal)
    {
        "ApiController",
        "ApiControllerAttribute",
        "Controller",
        "ControllerAttribute"
    };

    private static readonly Dictionary<string, HttpMethod> HttpMethodAttributes = new(StringComparer.Ordinal)
    {
        ["HttpGet"] = HttpMethod.Get,
        ["HttpGetAttribute"] = HttpMethod.Get,
        ["HttpPost"] = HttpMethod.Post,
        ["HttpPostAttribute"] = HttpMethod.Post,
        ["HttpPut"] = HttpMethod.Put,
        ["HttpPutAttribute"] = HttpMethod.Put,
        ["HttpDelete"] = HttpMethod.Delete,
        ["HttpDeleteAttribute"] = HttpMethod.Delete,
        ["HttpPatch"] = HttpMethod.Patch,
        ["HttpPatchAttribute"] = HttpMethod.Patch,
        ["HttpHead"] = HttpMethod.Head,
        ["HttpHeadAttribute"] = HttpMethod.Head,
        ["HttpOptions"] = HttpMethod.Options,
        ["HttpOptionsAttribute"] = HttpMethod.Options
    };

    private readonly AttributeAuthorizationExtractor _authExtractor = new();
    private readonly SecurityClassifier _classifier = new();

    public IEnumerable<Endpoint> Discover(SyntaxTree syntaxTree)
        => Discover(syntaxTree, GlobalAuthorizationInfo.Empty);

    public IEnumerable<Endpoint> Discover(SyntaxTree syntaxTree, GlobalAuthorizationInfo globalAuth)
    {
        var root = syntaxTree.GetCompilationUnitRoot();
        var filePath = syntaxTree.FilePath;

        foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (!IsController(classDecl))
                continue;

            var controllerName = GetControllerName(classDecl);
            var controllerRoute = GetControllerRoute(classDecl);
            var controllerAuth = _authExtractor.ExtractFromAttributes(classDecl.AttributeLists);

            // Apply global auth if controller has no explicit auth
            var effectiveControllerAuth = ApplyGlobalAuth(controllerAuth, globalAuth);

            foreach (var method in classDecl.Members.OfType<MethodDeclarationSyntax>())
            {
                if (!IsActionMethod(method))
                    continue;

                var endpoint = CreateEndpoint(
                    method,
                    controllerName,
                    controllerRoute,
                    effectiveControllerAuth,
                    filePath);

                if (endpoint != null)
                    yield return endpoint;
            }
        }
    }

    private static AuthorizationInfo ApplyGlobalAuth(AuthorizationInfo controllerAuth, GlobalAuthorizationInfo globalAuth)
    {
        // If controller already has explicit auth or AllowAnonymous, don't override
        if (controllerAuth.HasAuthorize || controllerAuth.HasAllowAnonymous)
            return controllerAuth;

        // If global FallbackPolicy protects all endpoints, inherit from it
        if (globalAuth.ProtectsAllEndpointsByDefault)
        {
            return new AuthorizationInfo
            {
                HasAuthorize = controllerAuth.HasAuthorize,
                HasAllowAnonymous = controllerAuth.HasAllowAnonymous,
                Roles = controllerAuth.Roles,
                Policies = controllerAuth.Policies,
                AuthenticationSchemes = controllerAuth.AuthenticationSchemes,
                InheritedFrom = globalAuth.ToAuthorizationInfo()
            };
        }

        return controllerAuth;
    }

    private bool IsController(ClassDeclarationSyntax classDecl)
    {
        // Check if class name ends with "Controller"
        if (classDecl.Identifier.Text.EndsWith("Controller", StringComparison.Ordinal))
            return true;

        // Check for [ApiController] or [Controller] attribute
        return classDecl.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(attr => ControllerAttributeNames.Contains(GetAttributeName(attr)));
    }

    private static string GetControllerName(ClassDeclarationSyntax classDecl)
    {
        var name = classDecl.Identifier.Text;
        if (name.EndsWith("Controller", StringComparison.Ordinal))
            name = name[..^"Controller".Length];
        return name;
    }

    private static string? GetControllerRoute(ClassDeclarationSyntax classDecl)
    {
        foreach (var attrList in classDecl.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = GetAttributeName(attr);
                if (name is "Route" or "RouteAttribute")
                {
                    return GetRouteTemplate(attr);
                }
            }
        }
        return null;
    }

    private static bool IsActionMethod(MethodDeclarationSyntax method)
    {
        // Must be public
        if (!method.Modifiers.Any(SyntaxKind.PublicKeyword))
            return false;

        // Skip static methods
        if (method.Modifiers.Any(SyntaxKind.StaticKeyword))
            return false;

        // Check for [NonAction] attribute
        var hasNonAction = method.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(attr =>
            {
                var name = GetAttributeName(attr);
                return name is "NonAction" or "NonActionAttribute";
            });

        return !hasNonAction;
    }

    private Endpoint? CreateEndpoint(
        MethodDeclarationSyntax method,
        string controllerName,
        string? controllerRoute,
        AuthorizationInfo controllerAuth,
        string filePath)
    {
        var actionName = method.Identifier.Text;
        var (httpMethods, actionRoute) = ExtractHttpMethodsAndRoute(method);

        // If no HTTP method attribute, default to GET for actions
        if (httpMethods == HttpMethod.None)
            httpMethods = HttpMethod.Get;

        var route = BuildRoute(controllerRoute, actionRoute, controllerName, actionName);
        var actionAuth = _authExtractor.ExtractFromAttributes(method.AttributeLists);
        var mergedAuth = _authExtractor.MergeWithController(actionAuth, controllerAuth);
        var classification = _classifier.Classify(mergedAuth);

        var location = method.GetLocation();
        var lineSpan = location.GetLineSpan();

        return new Endpoint
        {
            Route = route,
            Methods = httpMethods,
            Type = EndpointType.Controller,
            Location = new SourceLocation(
                filePath,
                lineSpan.StartLinePosition.Line + 1,
                lineSpan.StartLinePosition.Character + 1),
            ControllerName = controllerName,
            ActionName = actionName,
            Authorization = mergedAuth,
            Classification = classification
        };
    }

    private static (HttpMethod Methods, string? Route) ExtractHttpMethodsAndRoute(MethodDeclarationSyntax method)
    {
        var methods = HttpMethod.None;
        string? route = null;

        foreach (var attrList in method.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                var name = GetAttributeName(attr);

                if (HttpMethodAttributes.TryGetValue(name, out var httpMethod))
                {
                    methods |= httpMethod;
                    route ??= GetRouteTemplate(attr);
                }
                else if (name is "Route" or "RouteAttribute")
                {
                    route ??= GetRouteTemplate(attr);
                }
            }
        }

        return (methods, route);
    }

    private static string BuildRoute(
        string? controllerRoute,
        string? actionRoute,
        string controllerName,
        string actionName)
    {
        // Start with controller route or default
        var basePath = controllerRoute ?? $"api/{controllerName}";

        // Replace [controller] token
        basePath = basePath.Replace("[controller]", controllerName, StringComparison.OrdinalIgnoreCase);

        // Check if route already has [action] token before deciding to append action name
        var hasActionToken = basePath.Contains("[action]", StringComparison.OrdinalIgnoreCase) ||
                            (actionRoute?.Contains("[action]", StringComparison.OrdinalIgnoreCase) ?? false);

        // If action route specified, combine
        if (!string.IsNullOrEmpty(actionRoute))
        {
            if (actionRoute.StartsWith('/'))
            {
                // Absolute route
                basePath = actionRoute;
            }
            else
            {
                // Relative route
                basePath = $"{basePath.TrimEnd('/')}/{actionRoute}";
            }
        }
        else if (!hasActionToken)
        {
            // Add action name if no route specified, no [action] token, and not index
            if (!actionName.Equals("Index", StringComparison.OrdinalIgnoreCase))
            {
                basePath = $"{basePath.TrimEnd('/')}/{actionName}";
            }
        }

        // Replace [action] token
        basePath = basePath.Replace("[action]", actionName, StringComparison.OrdinalIgnoreCase);

        // Ensure starts with /
        if (!basePath.StartsWith('/'))
            basePath = "/" + basePath;

        return basePath;
    }

    private static string GetAttributeName(AttributeSyntax attribute)
    {
        return attribute.Name switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            QualifiedNameSyntax qn => qn.Right.Identifier.Text,
            AliasQualifiedNameSyntax aq => aq.Name.Identifier.Text,
            _ => attribute.Name.ToString()
        };
    }

    private static string? GetRouteTemplate(AttributeSyntax attribute)
    {
        if (attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count == 0)
            return null;

        var firstArg = attribute.ArgumentList.Arguments[0];

        // Template is typically the first positional argument
        if (firstArg.NameEquals == null && firstArg.NameColon == null)
        {
            return ExtractStringValue(firstArg.Expression);
        }

        // Or named Template argument
        foreach (var arg in attribute.ArgumentList.Arguments)
        {
            var paramName = arg.NameEquals?.Name.Identifier.Text ??
                            arg.NameColon?.Name.Identifier.Text;

            if (paramName is "Template" or "template")
            {
                return ExtractStringValue(arg.Expression);
            }
        }

        return null;
    }

    private static string? ExtractStringValue(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal => literal.Token.ValueText,
            _ => null
        };
    }
}
