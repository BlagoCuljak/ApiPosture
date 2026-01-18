namespace ApiPosture.Core.Models;

/// <summary>
/// Represents a location in source code.
/// </summary>
public sealed record SourceLocation(
    string FilePath,
    int LineNumber,
    int Column = 0)
{
    public override string ToString() => $"{FilePath}:{LineNumber}";
}
