using ApiPosture.Core.Models;

namespace ApiPosture.Core.Extensions;

/// <summary>
/// Interface for extensions that add analysis phases to the scan pipeline.
/// Analysis phases run after endpoint discovery and rule evaluation.
/// </summary>
public interface IAnalysisPhaseProvider
{
    /// <summary>
    /// Gets the analysis phases provided by this extension.
    /// </summary>
    IReadOnlyList<IAnalysisPhase> GetPhases();
}

/// <summary>
/// Represents an analysis phase that processes scan results.
/// Phases are executed in order by priority after the main scan completes.
/// </summary>
public interface IAnalysisPhase
{
    /// <summary>
    /// Unique identifier for this phase.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name for this phase.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Priority for execution order. Lower numbers execute first.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Executes the analysis phase.
    /// </summary>
    /// <param name="context">The analysis context with scan results.</param>
    /// <returns>Updated analysis context.</returns>
    AnalysisPhaseResult Execute(AnalysisPhaseContext context);
}

/// <summary>
/// Context provided to analysis phases.
/// </summary>
public sealed class AnalysisPhaseContext
{
    /// <summary>
    /// All discovered endpoints.
    /// </summary>
    public required IReadOnlyList<Endpoint> Endpoints { get; init; }

    /// <summary>
    /// All findings from rule evaluation.
    /// </summary>
    public required IReadOnlyList<Finding> Findings { get; init; }

    /// <summary>
    /// The scanned path.
    /// </summary>
    public required string ScannedPath { get; init; }

    /// <summary>
    /// Additional metadata that phases can use to pass data.
    /// </summary>
    public Dictionary<string, object> Metadata { get; } = new();
}

/// <summary>
/// Result from an analysis phase.
/// </summary>
public sealed class AnalysisPhaseResult
{
    /// <summary>
    /// Additional findings produced by this phase.
    /// </summary>
    public IReadOnlyList<Finding> AdditionalFindings { get; init; } = Array.Empty<Finding>();

    /// <summary>
    /// Metadata to add to the context for subsequent phases.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Creates an empty result.
    /// </summary>
    public static AnalysisPhaseResult Empty { get; } = new();
}
