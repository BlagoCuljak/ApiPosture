using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace ApiPosture.Core.Analysis;

/// <summary>
/// Loads and parses C# source files using Roslyn.
/// </summary>
public sealed class SourceFileLoader
{
    private readonly List<string> _failedFiles = [];

    /// <summary>
    /// Files that failed to parse.
    /// </summary>
    public IReadOnlyList<string> FailedFiles => _failedFiles;

    /// <summary>
    /// Discovers all C# source files in the given path.
    /// </summary>
    public IEnumerable<string> DiscoverSourceFiles(string path)
    {
        if (File.Exists(path) && path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.GetFullPath(path);
            yield break;
        }

        if (!Directory.Exists(path))
        {
            yield break;
        }

        var matcher = new Matcher();
        matcher.AddInclude("**/*.cs");
        matcher.AddExclude("**/obj/**");
        matcher.AddExclude("**/bin/**");

        var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(path));
        var result = matcher.Execute(directoryInfo);

        foreach (var file in result.Files)
        {
            yield return Path.GetFullPath(Path.Combine(path, file.Path));
        }
    }

    /// <summary>
    /// Parses a C# source file and returns its syntax tree.
    /// </summary>
    public SyntaxTree? ParseFile(string filePath)
    {
        try
        {
            var sourceText = File.ReadAllText(filePath);
            var options = new CSharpParseOptions(
                languageVersion: LanguageVersion.Latest,
                documentationMode: DocumentationMode.None,
                kind: SourceCodeKind.Regular);

            return CSharpSyntaxTree.ParseText(sourceText, options, filePath);
        }
        catch (Exception)
        {
            _failedFiles.Add(filePath);
            return null;
        }
    }

    /// <summary>
    /// Parses source code from a string (useful for testing).
    /// </summary>
    public static SyntaxTree ParseText(string sourceCode, string path = "test.cs")
    {
        var options = new CSharpParseOptions(
            languageVersion: LanguageVersion.Latest,
            documentationMode: DocumentationMode.None,
            kind: SourceCodeKind.Regular);

        return CSharpSyntaxTree.ParseText(sourceCode, options, path);
    }

    /// <summary>
    /// Loads and parses all C# files from the given path.
    /// </summary>
    public IEnumerable<SyntaxTree> LoadAll(string path)
    {
        foreach (var file in DiscoverSourceFiles(path))
        {
            var tree = ParseFile(file);
            if (tree != null)
            {
                yield return tree;
            }
        }
    }
}
