using ApiPosture.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ApiPosture.Core.Authorization;

/// <summary>
/// Extracts authorization information from attributes.
/// </summary>
public sealed class AttributeAuthorizationExtractor
{
    private static readonly HashSet<string> AuthorizeAttributeNames = new(StringComparer.Ordinal)
    {
        "Authorize",
        "AuthorizeAttribute",
        "Microsoft.AspNetCore.Authorization.Authorize",
        "Microsoft.AspNetCore.Authorization.AuthorizeAttribute"
    };

    private static readonly HashSet<string> AllowAnonymousAttributeNames = new(StringComparer.Ordinal)
    {
        "AllowAnonymous",
        "AllowAnonymousAttribute",
        "Microsoft.AspNetCore.Authorization.AllowAnonymous",
        "Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute"
    };

    /// <summary>
    /// Extracts authorization info from a member's attribute lists.
    /// </summary>
    public AuthorizationInfo ExtractFromAttributes(SyntaxList<AttributeListSyntax> attributeLists)
    {
        var hasAuthorize = false;
        var hasAllowAnonymous = false;
        var roles = new List<string>();
        var policies = new List<string>();
        var authSchemes = new List<string>();

        foreach (var attributeList in attributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = GetAttributeName(attribute);

                if (IsAuthorizeAttribute(name))
                {
                    hasAuthorize = true;
                    ExtractAuthorizeParameters(attribute, roles, policies, authSchemes);
                }
                else if (IsAllowAnonymousAttribute(name))
                {
                    hasAllowAnonymous = true;
                }
            }
        }

        return new AuthorizationInfo
        {
            HasAuthorize = hasAuthorize,
            HasAllowAnonymous = hasAllowAnonymous,
            Roles = roles,
            Policies = policies,
            AuthenticationSchemes = authSchemes
        };
    }

    /// <summary>
    /// Merges action-level auth with controller-level auth (inheritance).
    /// </summary>
    public AuthorizationInfo MergeWithController(
        AuthorizationInfo actionAuth,
        AuthorizationInfo controllerAuth)
    {
        return new AuthorizationInfo
        {
            HasAuthorize = actionAuth.HasAuthorize,
            HasAllowAnonymous = actionAuth.HasAllowAnonymous,
            Roles = actionAuth.Roles,
            Policies = actionAuth.Policies,
            AuthenticationSchemes = actionAuth.AuthenticationSchemes,
            InheritedFrom = controllerAuth
        };
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

    private static bool IsAuthorizeAttribute(string name)
    {
        return AuthorizeAttributeNames.Contains(name) ||
               name.StartsWith("Authorize", StringComparison.Ordinal);
    }

    private static bool IsAllowAnonymousAttribute(string name)
    {
        return AllowAnonymousAttributeNames.Contains(name) ||
               name.StartsWith("AllowAnonymous", StringComparison.Ordinal);
    }

    private static void ExtractAuthorizeParameters(
        AttributeSyntax attribute,
        List<string> roles,
        List<string> policies,
        List<string> authSchemes)
    {
        if (attribute.ArgumentList == null)
            return;

        foreach (var arg in attribute.ArgumentList.Arguments)
        {
            var paramName = arg.NameEquals?.Name.Identifier.Text ??
                            arg.NameColon?.Name.Identifier.Text;

            var value = ExtractStringValue(arg.Expression);
            if (string.IsNullOrEmpty(value))
                continue;

            switch (paramName)
            {
                case "Roles":
                    // Roles can be comma-separated
                    roles.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;

                case "Policy":
                    policies.Add(value);
                    break;

                case "AuthenticationSchemes":
                    authSchemes.AddRange(value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;

                case null:
                    // Positional argument - typically the policy
                    policies.Add(value);
                    break;
            }
        }
    }

    private static string? ExtractStringValue(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal => literal.Token.ValueText,
            InterpolatedStringExpressionSyntax interpolated => interpolated.ToString(),
            _ => null
        };
    }
}
