using System.Text.Json;
using System.Text.Json.Serialization;
using ApiPosture.Core.Models;

namespace ApiPosture.Output;

/// <summary>
/// Formats scan results as JSON.
/// </summary>
public sealed class JsonFormatter : IOutputFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Format(ScanResult result, Severity minSeverity)
    {
        var output = new JsonOutput
        {
            Summary = new SummaryInfo
            {
                ScannedPath = result.ScannedPath,
                FilesScanned = result.ScannedFiles.Count,
                FailedFiles = result.FailedFiles.Count,
                EndpointsFound = result.Endpoints.Count,
                TotalFindings = result.Findings.Count,
                DurationMs = result.Duration.TotalMilliseconds
            },
            Endpoints = result.Endpoints.Select(e => new EndpointInfo
            {
                Route = e.Route,
                Methods = e.MethodsDisplay,
                Type = e.Type.ToString(),
                Classification = e.Classification.ToString(),
                Location = e.Location.ToString(),
                ControllerName = e.ControllerName,
                ActionName = e.ActionName
            }).ToList(),
            Findings = result.GetFindingsBySeverity(minSeverity).Select(f => new FindingInfo
            {
                RuleId = f.RuleId,
                RuleName = f.RuleName,
                Severity = f.Severity.ToString(),
                Route = f.Endpoint.Route,
                Location = f.Endpoint.Location.ToString(),
                Message = f.Message,
                Recommendation = f.Recommendation
            }).ToList(),
            SeverityCounts = result.Findings
                .GroupBy(f => f.Severity)
                .ToDictionary(g => g.Key.ToString(), g => g.Count())
        };

        return JsonSerializer.Serialize(output, Options);
    }

    private sealed class JsonOutput
    {
        public required SummaryInfo Summary { get; init; }
        public required List<EndpointInfo> Endpoints { get; init; }
        public required List<FindingInfo> Findings { get; init; }
        public required Dictionary<string, int> SeverityCounts { get; init; }
    }

    private sealed class SummaryInfo
    {
        public required string ScannedPath { get; init; }
        public required int FilesScanned { get; init; }
        public required int FailedFiles { get; init; }
        public required int EndpointsFound { get; init; }
        public required int TotalFindings { get; init; }
        public required double DurationMs { get; init; }
    }

    private sealed class EndpointInfo
    {
        public required string Route { get; init; }
        public required string Methods { get; init; }
        public required string Type { get; init; }
        public required string Classification { get; init; }
        public required string Location { get; init; }
        public string? ControllerName { get; init; }
        public string? ActionName { get; init; }
    }

    private sealed class FindingInfo
    {
        public required string RuleId { get; init; }
        public required string RuleName { get; init; }
        public required string Severity { get; init; }
        public required string Route { get; init; }
        public required string Location { get; init; }
        public required string Message { get; init; }
        public required string Recommendation { get; init; }
    }
}
