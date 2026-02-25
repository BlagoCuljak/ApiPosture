using ApiPosture.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ApiPosture.Core.Analysis;

/// <summary>
/// Extracts attributes from source code for controller classes and action methods.
/// </summary>
public static class AttributeExtractor
{
    // Well-known framework base classes that don't carry custom authorization
    private static readonly HashSet<string> FrameworkBaseClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Controller", "ControllerBase", "ApiController", "PageModel",
        "RazorPage", "BaseController", "BaseApiController", "Hub"
    };

    /// <summary>
    /// Extracts all attribute names applied to the endpoint (both class-level and method-level),
    /// including attributes inherited from base classes in the same project.
    /// </summary>
    /// <param name="endpoint">The endpoint to extract attributes from</param>
    /// <returns>List of attribute names (without the "Attribute" suffix)</returns>
    public static IReadOnlyList<string> GetEndpointAttributes(Endpoint endpoint)
    {
        try
        {
            var filePath = endpoint.Location.FilePath;
            if (!File.Exists(filePath))
                return Array.Empty<string>();

            var sourceCode = File.ReadAllText(filePath);
            var tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetRoot();

            var attributes = new List<string>();

            // For controller endpoints, find the class and method
            if (endpoint.Type == EndpointType.Controller)
            {
                // Find controller class
                var classDecl = root.DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .FirstOrDefault(c =>
                        c.Identifier.Text == endpoint.ControllerName ||
                        c.Identifier.Text == $"{endpoint.ControllerName}Controller");

                if (classDecl != null)
                {
                    // Add class-level attributes
                    attributes.AddRange(ExtractAttributeNames(classDecl.AttributeLists));

                    // Traverse base class hierarchy to find inherited attributes
                    attributes.AddRange(ExtractBaseClassAttributes(classDecl, filePath, depth: 0));

                    // Find action method
                    if (endpoint.ActionName != null)
                    {
                        var methodDecl = classDecl.DescendantNodes()
                            .OfType<MethodDeclarationSyntax>()
                            .FirstOrDefault(m => m.Identifier.Text == endpoint.ActionName);

                        if (methodDecl != null)
                        {
                            // Add method-level attributes
                            attributes.AddRange(ExtractAttributeNames(methodDecl.AttributeLists));
                        }
                    }
                }
            }

            return attributes.Distinct().ToList();
        }
        catch
        {
            // If parsing fails, return empty list
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Extracts attributes from the base class hierarchy, searching project files.
    /// Stops at well-known framework base classes to avoid false positives.
    /// </summary>
    private static IEnumerable<string> ExtractBaseClassAttributes(
        ClassDeclarationSyntax classDecl,
        string sourceFilePath,
        int depth)
    {
        // Guard against deep or circular inheritance
        if (depth >= 5 || classDecl.BaseList == null)
            return [];

        var projectRoot = FindProjectRoot(sourceFilePath);
        if (projectRoot == null)
            return [];

        var results = new List<string>();

        foreach (var baseType in classDecl.BaseList.Types)
        {
            var baseName = ExtractSimpleName(baseType.Type.ToString());

            // Skip known framework classes and interfaces (interfaces start with 'I' + uppercase by convention)
            if (FrameworkBaseClasses.Contains(baseName))
                continue;
            if (baseName.Length > 1 && baseName[0] == 'I' && char.IsUpper(baseName[1]))
                continue;

            // Search for the base class file in the project
            var baseFile = FindClassFile(baseName, projectRoot);
            if (baseFile == null)
                continue;

            string baseSource;
            try { baseSource = File.ReadAllText(baseFile); }
            catch { continue; }

            CompilationUnitSyntax? baseRoot;
            try
            {
                var baseTree = CSharpSyntaxTree.ParseText(baseSource);
                baseRoot = (CompilationUnitSyntax)baseTree.GetRoot();
            }
            catch { continue; }

            var baseClassDecl = baseRoot.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .FirstOrDefault(c =>
                    c.Identifier.Text.Equals(baseName, StringComparison.OrdinalIgnoreCase) ||
                    c.Identifier.Text.Equals($"{baseName}Controller", StringComparison.OrdinalIgnoreCase));

            if (baseClassDecl == null)
                continue;

            // Add base class attributes
            results.AddRange(ExtractAttributeNames(baseClassDecl.AttributeLists));

            // Recurse into grandparent classes
            results.AddRange(ExtractBaseClassAttributes(baseClassDecl, baseFile, depth + 1));
        }

        return results;
    }

    /// <summary>
    /// Extracts the simple class name from a potentially qualified type name.
    /// E.g. "Namespace.BaseController" → "BaseController"
    /// </summary>
    private static string ExtractSimpleName(string typeName)
    {
        var lastDot = typeName.LastIndexOf('.');
        var name = lastDot >= 0 ? typeName.Substring(lastDot + 1) : typeName;

        // Remove generic type arguments
        var genericStart = name.IndexOf('<');
        return genericStart >= 0 ? name.Substring(0, genericStart) : name;
    }

    /// <summary>
    /// Finds the .csproj project root by walking up from a source file path.
    /// </summary>
    private static string? FindProjectRoot(string sourceFilePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(sourceFilePath);
            while (directory != null)
            {
                if (Directory.GetFiles(directory, "*.csproj").Length > 0)
                    return directory;
                directory = Path.GetDirectoryName(directory);
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Searches the project directory for a class file by class name.
    /// </summary>
    private static string? FindClassFile(string className, string projectRoot)
    {
        try
        {
            // Try exact name first, then with "Controller" suffix
            foreach (var pattern in new[] { $"{className}.cs", $"{className}Controller.cs" })
            {
                var files = Directory.GetFiles(projectRoot, pattern, SearchOption.AllDirectories);
                if (files.Length > 0)
                    return files[0];
            }
        }
        catch { }
        return null;
    }

    private static IEnumerable<string> ExtractAttributeNames(SyntaxList<AttributeListSyntax> attributeLists)
    {
        foreach (var attributeList in attributeLists)
        {
            foreach (var attribute in attributeList.Attributes)
            {
                var name = attribute.Name.ToString();

                // Remove "Attribute" suffix if present
                if (name.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(0, name.Length - "Attribute".Length);
                }

                yield return name;
            }
        }
    }
}
