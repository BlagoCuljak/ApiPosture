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
        // Use project root as part of cache key to handle same attribute names in different projects
        var projectRoot = FindProjectRoot(sourceFilePath) ?? sourceFilePath;
        return $"{attributeName}:{projectRoot}";
    }

    private bool AnalyzeAttributeImplementation(string attributeName, string sourceFilePath)
    {
        try
        {
            // Find project root
            var projectRoot = FindProjectRoot(sourceFilePath);
            if (projectRoot == null) return false;

            // Search for attribute class definition
            var attributeFile = FindAttributeClassFile(attributeName, projectRoot);
            if (attributeFile == null) return false;

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
                    return AnalyzeFilterType(filterType, projectRoot);
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
