using ApiPosture.Core.Models;

namespace ApiPosture.Output;

/// <summary>
/// Interface for formatting scan results.
/// </summary>
public interface IOutputFormatter
{
    /// <summary>
    /// Formats the scan result for output.
    /// </summary>
    string Format(ScanResult result, Severity minSeverity);
}
