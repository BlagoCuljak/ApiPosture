using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ApiPosture.Core.Authorization;

/// <summary>
/// Analyzes project files for global authorization configuration like FallbackPolicy.
/// </summary>
public sealed class GlobalAuthorizationAnalyzer
{
    /// <summary>
    /// Analyzes all syntax trees for global authorization configuration.
    /// </summary>
    public GlobalAuthorizationInfo Analyze(IEnumerable<SyntaxTree> syntaxTrees)
    {
        var hasFallbackPolicy = false;
        var fallbackRequiresAuth = false;
        var fallbackRoles = new List<string>();
        var fallbackClaims = new List<string>();
        var hasDefaultPolicy = false;

        foreach (var tree in syntaxTrees)
        {
            var root = tree.GetCompilationUnitRoot();

            // Find AddAuthorization calls
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsAddAuthorizationCall(invocation))
                    continue;

                // Look for lambda argument
                var lambdaArg = invocation.ArgumentList.Arguments
                    .Select(a => a.Expression)
                    .OfType<SimpleLambdaExpressionSyntax>()
                    .FirstOrDefault();

                if (lambdaArg == null)
                {
                    // Try parenthesized lambda
                    var parenLambda = invocation.ArgumentList.Arguments
                        .Select(a => a.Expression)
                        .OfType<ParenthesizedLambdaExpressionSyntax>()
                        .FirstOrDefault();

                    if (parenLambda != null)
                    {
                        AnalyzeLambdaBody(parenLambda.Body, ref hasFallbackPolicy, ref fallbackRequiresAuth,
                            fallbackRoles, fallbackClaims, ref hasDefaultPolicy);
                    }
                }
                else
                {
                    AnalyzeLambdaBody(lambdaArg.Body, ref hasFallbackPolicy, ref fallbackRequiresAuth,
                        fallbackRoles, fallbackClaims, ref hasDefaultPolicy);
                }
            }
        }

        return new GlobalAuthorizationInfo
        {
            HasFallbackPolicy = hasFallbackPolicy,
            FallbackRequiresAuthentication = fallbackRequiresAuth,
            FallbackRoles = fallbackRoles.Distinct().ToList(),
            FallbackClaims = fallbackClaims.Distinct().ToList(),
            HasDefaultPolicy = hasDefaultPolicy
        };
    }

    private static bool IsAddAuthorizationCall(InvocationExpressionSyntax invocation)
    {
        var methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };

        return methodName == "AddAuthorization";
    }

    private void AnalyzeLambdaBody(
        SyntaxNode? body,
        ref bool hasFallbackPolicy,
        ref bool fallbackRequiresAuth,
        List<string> fallbackRoles,
        List<string> fallbackClaims,
        ref bool hasDefaultPolicy)
    {
        if (body == null)
            return;

        // Look for options.FallbackPolicy = ... or options.DefaultPolicy = ...
        foreach (var assignment in body.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is MemberAccessExpressionSyntax memberAccess)
            {
                var propertyName = memberAccess.Name.Identifier.Text;

                if (propertyName == "FallbackPolicy")
                {
                    hasFallbackPolicy = true;
                    AnalyzePolicyBuilder(assignment.Right, ref fallbackRequiresAuth, fallbackRoles, fallbackClaims);
                }
                else if (propertyName == "DefaultPolicy")
                {
                    hasDefaultPolicy = true;
                }
            }
        }
    }

    private void AnalyzePolicyBuilder(
        ExpressionSyntax expression,
        ref bool requiresAuth,
        List<string> roles,
        List<string> claims)
    {
        // Handle new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
        // or similar fluent chain patterns

        // Walk through invocations looking for policy builder methods
        var invocations = expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            var methodName = invocation.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
                IdentifierNameSyntax identifier => identifier.Identifier.Text,
                _ => null
            };

            switch (methodName)
            {
                case "RequireAuthenticatedUser":
                    requiresAuth = true;
                    break;

                case "RequireRole":
                    ExtractStringArguments(invocation, roles);
                    break;

                case "RequireClaim":
                    ExtractFirstStringArgument(invocation, claims);
                    break;
            }
        }
    }

    private static void ExtractStringArguments(InvocationExpressionSyntax invocation, List<string> values)
    {
        foreach (var arg in invocation.ArgumentList.Arguments)
        {
            if (arg.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                values.Add(literal.Token.ValueText);
            }
            // Handle params array
            else if (arg.Expression is ImplicitArrayCreationExpressionSyntax implicitArray)
            {
                foreach (var element in implicitArray.Initializer.Expressions)
                {
                    if (element is LiteralExpressionSyntax arrayLiteral &&
                        arrayLiteral.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        values.Add(arrayLiteral.Token.ValueText);
                    }
                }
            }
            else if (arg.Expression is ArrayCreationExpressionSyntax arrayCreation &&
                     arrayCreation.Initializer != null)
            {
                foreach (var element in arrayCreation.Initializer.Expressions)
                {
                    if (element is LiteralExpressionSyntax arrayLiteral &&
                        arrayLiteral.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        values.Add(arrayLiteral.Token.ValueText);
                    }
                }
            }
            // Handle collection expressions in modern C#
            else if (arg.Expression is CollectionExpressionSyntax collection)
            {
                foreach (var element in collection.Elements)
                {
                    if (element is ExpressionElementSyntax exprElement &&
                        exprElement.Expression is LiteralExpressionSyntax collLiteral &&
                        collLiteral.IsKind(SyntaxKind.StringLiteralExpression))
                    {
                        values.Add(collLiteral.Token.ValueText);
                    }
                }
            }
        }
    }

    private static void ExtractFirstStringArgument(InvocationExpressionSyntax invocation, List<string> values)
    {
        if (invocation.ArgumentList.Arguments.Count > 0)
        {
            var firstArg = invocation.ArgumentList.Arguments[0].Expression;
            if (firstArg is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                values.Add(literal.Token.ValueText);
            }
        }
    }
}
