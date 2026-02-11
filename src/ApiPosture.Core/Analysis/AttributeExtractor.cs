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
    /// <summary>
    /// Extracts all attribute names applied to the endpoint (both class-level and method-level).
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
                    .FirstOrDefault(c => c.Identifier.Text == endpoint.ControllerName);

                if (classDecl != null)
                {
                    // Add class-level attributes
                    attributes.AddRange(ExtractAttributeNames(classDecl.AttributeLists));

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
