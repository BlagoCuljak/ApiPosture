using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ApiPosture.Core.Analysis;

/// <summary>
/// Analyzes custom attributes using Roslyn to determine if they implement authorization.
/// Results are cached per attribute+project for performance.
/// </summary>
public sealed class AuthorizationAttributeAnalyzer : IAuthorizationAttributeAnalyzer
{
    private readonly ConcurrentDictionary<string, bool> _cache = new();

    /// <inheritdoc />
    public bool IsAuthorizationAttribute(string attributeName, string sourceFilePath)
    {
        // Normalize attribute name (remove "Attribute" suffix if present)
        var normalizedName = attributeName.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase)
            ? attributeName.Substring(0, attributeName.Length - "Attribute".Length)
            : attributeName;

        // Create cache key: attributeName + projectPath
        var cacheKey = GetCacheKey(normalizedName, sourceFilePath);

        return _cache.GetOrAdd(cacheKey, _ =>
            AnalyzeAttributeImplementation(normalizedName, sourceFilePath));
    }

    private string GetCacheKey(string attributeName, string sourceFilePath)
    {
        // Use solution root (or project root) as part of cache key
        var root = FindSolutionRoot(sourceFilePath) ?? FindProjectRoot(sourceFilePath) ?? sourceFilePath;
        return $"{attributeName}:{root}";
    }

    private bool AnalyzeAttributeImplementation(string attributeName, string sourceFilePath)
    {
        try
        {
            // Find search root - prefer solution root for cross-project attribute discovery,
            // fall back to project root
            var projectRoot = FindProjectRoot(sourceFilePath);
            var searchRoot = FindSolutionRoot(sourceFilePath) ?? projectRoot;
            if (searchRoot == null) return false;

            // Search for attribute class definition across the entire solution
            var attributeFile = FindAttributeClassFile(attributeName, searchRoot);
            if (attributeFile == null)
            {
                // Fallback: use naming convention heuristics when source isn't available
                // (e.g., attribute defined in NuGet package or external assembly)
                return IsAuthorizationByNamingConvention(attributeName);
            }

            // Parse with Roslyn
            var sourceCode = File.ReadAllText(attributeFile);
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            // Find class declaration (try both with and without "Attribute" suffix)
            var classDecl = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c =>
                    c.Identifier.Text.Equals(attributeName, StringComparison.OrdinalIgnoreCase) ||
                    c.Identifier.Text.Equals($"{attributeName}Attribute", StringComparison.OrdinalIgnoreCase));

            if (classDecl == null) return false;

            // Check base types and interfaces
            if (ImplementsAuthorizationInterface(classDecl))
                return true;

            // Check if it's TypeFilterAttribute - extract and analyze filter type
            if (InheritsFromTypeFilterAttribute(classDecl))
            {
                var filterType = ExtractFilterType(classDecl);
                if (filterType != null)
                {
                    return AnalyzeFilterType(filterType, searchRoot);
                }
            }

            return false;
        }
        catch
        {
            // If analysis fails, assume not an auth attribute (fail-safe)
            return false;
        }
    }

    private bool ImplementsAuthorizationInterface(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.BaseList == null)
            return false;

        var baseTypes = classDecl.BaseList.Types
            .Select(t => t.ToString())
            .ToList();

        // Check for authorization-related interfaces and base classes
        return baseTypes.Any(b =>
            b.Contains("IAuthorizationFilter", StringComparison.OrdinalIgnoreCase) ||
            b.Contains("IAsyncAuthorizationFilter", StringComparison.OrdinalIgnoreCase) ||
            b.Contains("AuthorizeAttribute", StringComparison.OrdinalIgnoreCase));
    }

    private bool InheritsFromTypeFilterAttribute(ClassDeclarationSyntax classDecl)
    {
        if (classDecl.BaseList == null)
            return false;

        var baseTypes = classDecl.BaseList.Types
            .Select(t => t.ToString())
            .ToList();

        return baseTypes.Any(b => b.Contains("TypeFilterAttribute", StringComparison.OrdinalIgnoreCase));
    }

    private string? ExtractFilterType(ClassDeclarationSyntax classDecl)
    {
        // Find constructor that calls base(typeof(FilterType))
        var constructor = classDecl.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .FirstOrDefault();

        if (constructor == null) return null;

        // Look for base initializer: base(typeof(ApiKeyFilter))
        var baseInitializer = constructor.Initializer;
        if (baseInitializer == null) return null;

        // Find typeof expression in arguments
        var typeofExpression = baseInitializer.DescendantNodes()
            .OfType<TypeOfExpressionSyntax>()
            .FirstOrDefault();

        return typeofExpression?.Type.ToString();
    }

    private bool AnalyzeFilterType(string filterTypeName, string projectRoot)
    {
        try
        {
            // Find and analyze the filter class
            var filterFile = FindClassFile(filterTypeName, projectRoot);
            if (filterFile == null) return false;

            var sourceCode = File.ReadAllText(filterFile);
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            var classDecl = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c => c.Identifier.Text.Equals(filterTypeName, StringComparison.OrdinalIgnoreCase));

            return classDecl != null && ImplementsAuthorizationInterface(classDecl);
        }
        catch
        {
            return false;
        }
    }

    private string? FindAttributeClassFile(string attributeName, string projectRoot)
    {
        // Search patterns: ApiKeyAttribute.cs, ApiKey.cs
        var searchPatterns = new[]
        {
            $"{attributeName}Attribute.cs",
            $"{attributeName}.cs"
        };

        foreach (var pattern in searchPatterns)
        {
            try
            {
                var files = Directory.GetFiles(projectRoot, pattern, SearchOption.AllDirectories);
                if (files.Length > 0)
                    return files[0];
            }
            catch
            {
                // Continue to next pattern if search fails
            }
        }

        return null;
    }

    private string? FindClassFile(string className, string projectRoot)
    {
        try
        {
            var files = Directory.GetFiles(projectRoot, $"{className}.cs", SearchOption.AllDirectories);
            return files.Length > 0 ? files[0] : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if an attribute name matches common authorization naming conventions.
    /// Used as a fallback when the attribute source code cannot be found (e.g., NuGet packages,
    /// external assemblies, or attributes in projects outside the solution).
    /// </summary>
    private static bool IsAuthorizationByNamingConvention(string attributeName)
    {
        var name = attributeName;

        // Exact matches for well-known custom auth attribute names
        var knownAuthAttributes = new[]
        {
            "ApiKey", "ServiceFilter", "TypeFilter",
            "RequireHttps", "RequiresClaim", "RequiresRole",
        };

        if (knownAuthAttributes.Any(a => name.Equals(a, StringComparison.OrdinalIgnoreCase)))
            return true;

        // Naming patterns that strongly indicate authorization purpose
        // Matches: "ApiKeyAuth", "JwtAuthorize", "TokenAuthentication", "CustomAuthorizationFilter"
        var authPatterns = new[]
        {
            "Auth",     // Covers Authorize, Authorization, Authenticated, CustomAuth, etc.
            "ApiKey",   // Covers ApiKeyRequired, ApiKeyValidation, etc.
            "Token",    // Covers TokenValidation, BearerToken, etc.
            "Jwt",      // Covers JwtBearer, JwtAuth, etc.
            "Bearer",   // Covers BearerAuth, etc.
            "OAuth",    // Covers OAuth2, OAuthBearer, etc.
            "Oidc",     // Covers OidcAuth, etc.
            "Permission", // Covers RequirePermission, HasPermission, etc.
            "Hmac",     // Covers HmacAuth, HmacSignature, etc.
        };

        return authPatterns.Any(p =>
            name.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds the solution root directory by searching upward for a .sln file.
    /// This enables cross-project attribute discovery within a solution.
    /// </summary>
    private string? FindSolutionRoot(string sourceFilePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(sourceFilePath);
            while (directory != null)
            {
                if (Directory.GetFiles(directory, "*.sln").Length > 0)
                    return directory;

                directory = Path.GetDirectoryName(directory);
            }
        }
        catch
        {
            // Return null if we can't determine solution root
        }

        return null;
    }

    private string? FindProjectRoot(string sourceFilePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(sourceFilePath);
            while (directory != null)
            {
                // Look for .csproj file
                if (Directory.GetFiles(directory, "*.csproj").Length > 0)
                    return directory;

                directory = Path.GetDirectoryName(directory);
            }
        }
        catch
        {
            // Return null if we can't determine project root
        }

        return null;
    }
}
