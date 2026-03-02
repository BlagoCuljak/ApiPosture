using ApiPosture.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ApiPosture.Core.Discovery;

/// <summary>
/// Represents a route group registration with its prefix, authorization, and extension method calls.
/// </summary>
public sealed class RouteGroupInfo
{
    /// <summary>
    /// The variable name holding the route group (e.g., "apiGroup").
    /// </summary>
    public required string VariableName { get; init; }

    /// <summary>
    /// The route prefix for this group (e.g., "/api/v1").
    /// </summary>
    public required string RoutePrefix { get; init; }

    /// <summary>
    /// Authorization info applied to this group.
    /// </summary>
    public AuthorizationInfo Authorization { get; init; } = AuthorizationInfo.Empty;

    /// <summary>
    /// Extension methods called on this group (e.g., "MapTasksRoutes", "MapUsersRoutes").
    /// </summary>
    public IReadOnlyList<string> ExtensionMethodCalls { get; init; } = [];

    /// <summary>
    /// The file path where this group is defined.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;
}

/// <summary>
/// Collects and tracks route groups and their extension method calls across the project.
/// </summary>
public sealed class RouteGroupRegistry
{
    private readonly List<RouteGroupInfo> _routeGroups = new();

    /// <summary>
    /// All registered route groups.
    /// </summary>
    public IReadOnlyList<RouteGroupInfo> RouteGroups => _routeGroups;

    /// <summary>
    /// Analyzes a syntax tree for route group definitions and their extension method calls.
    /// </summary>
    public void Analyze(SyntaxTree syntaxTree)
    {
        var root = syntaxTree.GetCompilationUnitRoot();
        var filePath = syntaxTree.FilePath;

        // Find variable declarations with MapGroup
        foreach (var declaration in root.DescendantNodes().OfType<LocalDeclarationStatementSyntax>())
        {
            AnalyzeLocalDeclaration(declaration, root, filePath);
        }

        // Also check field declarations in classes
        foreach (var fieldDecl in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            AnalyzeFieldDeclaration(fieldDecl, root, filePath);
        }

        // Find direct Map*Routes/Map*Endpoints calls on the app/builder itself (not on a MapGroup variable).
        // e.g. app.MapRoleEndpoints() or app.MapTasksRoutes() called directly on WebApplication/IEndpointRouteBuilder.
        // These are registered as root-level groups with an empty prefix so that the extension method
        // discoverer can still process them and pick up the MapGroup + auth chain inside each method body.
        AnalyzeDirectExtensionMethodCalls(root, filePath);
    }

    private void AnalyzeLocalDeclaration(LocalDeclarationStatementSyntax declaration, CompilationUnitSyntax root, string filePath)
    {
        foreach (var variable in declaration.Declaration.Variables)
        {
            if (variable.Initializer?.Value is not InvocationExpressionSyntax invocation)
                continue;

            var routePrefix = ExtractMapGroupRoute(invocation);
            if (routePrefix == null)
                continue;

            var variableName = variable.Identifier.Text;
            var auth = AnalyzeGroupAuthorization(invocation);
            var extensionCalls = FindExtensionMethodCalls(variableName, root);

            _routeGroups.Add(new RouteGroupInfo
            {
                VariableName = variableName,
                RoutePrefix = routePrefix,
                Authorization = auth,
                ExtensionMethodCalls = extensionCalls,
                FilePath = filePath
            });
        }
    }

    private void AnalyzeFieldDeclaration(FieldDeclarationSyntax fieldDecl, CompilationUnitSyntax root, string filePath)
    {
        foreach (var variable in fieldDecl.Declaration.Variables)
        {
            if (variable.Initializer?.Value is not InvocationExpressionSyntax invocation)
                continue;

            var routePrefix = ExtractMapGroupRoute(invocation);
            if (routePrefix == null)
                continue;

            var variableName = variable.Identifier.Text;
            var auth = AnalyzeGroupAuthorization(invocation);
            var extensionCalls = FindExtensionMethodCalls(variableName, root);

            _routeGroups.Add(new RouteGroupInfo
            {
                VariableName = variableName,
                RoutePrefix = routePrefix,
                Authorization = auth,
                ExtensionMethodCalls = extensionCalls,
                FilePath = filePath
            });
        }
    }

    /// <summary>
    /// Scans for Map*Routes / Map*Endpoints calls made directly on a non-MapGroup receiver
    /// (e.g. <c>app.MapRoleEndpoints()</c> where <c>app</c> is a WebApplication or
    /// IEndpointRouteBuilder that was NOT produced by MapGroup).
    /// Each unique method name found is registered as a root-level group (empty prefix, no
    /// inherited auth) so that Phase 6 of ProjectAnalyzer can still correlate it with the
    /// matching extension method and discover the endpoints declared inside that method.
    /// </summary>
    private void AnalyzeDirectExtensionMethodCalls(CompilationUnitSyntax root, string filePath)
    {
        // Collect all variable names that are already known MapGroup variables so we can
        // skip them — they are already handled by AnalyzeLocalDeclaration/AnalyzeFieldDeclaration.
        var knownGroupVariables = new HashSet<string>(
            _routeGroups.Select(g => g.VariableName),
            StringComparer.Ordinal);

        // Track which method names we have already registered from this file so we only
        // create one synthetic RouteGroupInfo per method name (multiple call sites for the
        // same method would otherwise produce duplicates).
        var registeredMethods = new HashSet<string>(StringComparer.Ordinal);

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
                continue;

            var methodName = memberAccess.Name.Identifier.Text;

            // Must match Map*Routes or Map*Endpoints
            if (!methodName.StartsWith("Map", StringComparison.Ordinal) ||
                (!methodName.EndsWith("Routes", StringComparison.Ordinal) &&
                 !methodName.EndsWith("Endpoints", StringComparison.Ordinal)))
                continue;

            // Skip if already registered from this scan
            if (!registeredMethods.Add(methodName))
                continue;

            // Skip if the receiver is a known MapGroup variable (already handled above)
            if (memberAccess.Expression is IdentifierNameSyntax receiverIdentifier &&
                knownGroupVariables.Contains(receiverIdentifier.Identifier.Text))
                continue;

            // Register a synthetic root-level group: empty prefix, no inherited auth.
            // The extension method body itself will supply its own MapGroup + auth chain.
            // Any method name that doesn't correspond to a discovered RouteExtensionMethod
            // will simply produce no endpoints in Phase 6 — no need to pre-filter here.
            _routeGroups.Add(new RouteGroupInfo
            {
                VariableName = $"__direct__{methodName}",
                RoutePrefix = string.Empty,
                Authorization = AuthorizationInfo.Empty,
                ExtensionMethodCalls = [methodName],
                FilePath = filePath
            });
        }
    }

    private static string? ExtractMapGroupRoute(InvocationExpressionSyntax invocation)
    {
        // Walk the fluent chain to find MapGroup
        var current = invocation;
        while (current != null)
        {
            var methodName = current.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                _ => null
            };

            if (methodName == "MapGroup" && current.ArgumentList.Arguments.Count > 0)
            {
                var firstArg = current.ArgumentList.Arguments[0].Expression;
                if (firstArg is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
                {
                    return literal.Token.ValueText;
                }
            }

            // Check if there's a nested invocation (fluent chain)
            current = current.Expression switch
            {
                MemberAccessExpressionSyntax ma when ma.Expression is InvocationExpressionSyntax inv => inv,
                _ => null
            };
        }

        return null;
    }

    private static AuthorizationInfo AnalyzeGroupAuthorization(InvocationExpressionSyntax invocation)
    {
        var hasRequireAuth = false;
        var hasAllowAnonymous = false;
        var roles = new List<string>();
        var policies = new List<string>();

        // Walk the fluent chain (from outer to inner) looking for auth methods
        // E.g., app.MapGroup("/admin").RequireAuthorization("AdminPolicy")
        // The invocation IS the outermost call (RequireAuthorization in this case)
        InvocationExpressionSyntax? current = invocation;
        while (current != null)
        {
            var methodName = current.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                _ => null
            };

            switch (methodName)
            {
                case "RequireAuthorization":
                    hasRequireAuth = true;
                    ExtractAuthorizationArgs(current, policies, roles);
                    break;

                case "AllowAnonymous":
                    hasAllowAnonymous = true;
                    break;

                case "RequireRole":
                    hasRequireAuth = true;
                    ExtractRolesFromCall(current, roles);
                    break;
            }

            // Move to the inner invocation in the chain
            current = current.Expression switch
            {
                MemberAccessExpressionSyntax ma when ma.Expression is InvocationExpressionSyntax inv => inv,
                _ => null
            };
        }

        return new AuthorizationInfo
        {
            HasAuthorize = hasRequireAuth,
            HasAllowAnonymous = hasAllowAnonymous,
            Roles = roles,
            Policies = policies
        };
    }

    private static void ExtractAuthorizationArgs(InvocationExpressionSyntax invocation, List<string> policies, List<string> roles)
    {
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                policies.Add(literal.Token.ValueText);
            }
        }
    }

    private static void ExtractRolesFromCall(InvocationExpressionSyntax invocation, List<string> roles)
    {
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                roles.Add(literal.Token.ValueText);
            }
        }
    }

    private static List<string> FindExtensionMethodCalls(string variableName, SyntaxNode root)
    {
        var extensionCalls = new List<string>();

        // Find invocations like: variableName.MapSomethingRoutes()
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Expression is IdentifierNameSyntax identifier &&
                identifier.Identifier.Text == variableName)
            {
                var methodName = memberAccess.Name.Identifier.Text;

                // Filter to extension methods that look like route registrations
                // (Map*Routes or Map*Endpoints pattern)
                if (methodName.StartsWith("Map", StringComparison.Ordinal) &&
                    (methodName.EndsWith("Routes", StringComparison.Ordinal) ||
                     methodName.EndsWith("Endpoints", StringComparison.Ordinal)))
                {
                    extensionCalls.Add(methodName);
                }
            }
        }

        return extensionCalls;
    }

    /// <summary>
    /// Gets route groups that call a specific extension method.
    /// </summary>
    public IEnumerable<RouteGroupInfo> GetGroupsCallingMethod(string methodName)
    {
        return _routeGroups.Where(g => g.ExtensionMethodCalls.Contains(methodName));
    }
}
