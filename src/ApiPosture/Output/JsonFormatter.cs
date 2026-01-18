using System.Text.Json;
using System.Text.Json.Serialization;
using ApiPosture.Core.Filtering;
using ApiPosture.Core.Grouping;
using ApiPosture.Core.Models;
using ApiPosture.Core.Sorting;

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
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string Format(ScanResult result, OutputOptions options)
    {
        // Apply filtering
        var endpointFilter = new EndpointFilter(options.FilterOptions);
        var findingFilter = new FindingFilter(options.FilterOptions);

        var filteredEndpoints = endpointFilter.Filter(result.Endpoints).ToList();
        var filteredFindings = findingFilter.Filter(result.GetFindingsBySeverity(options.MinSeverity)).ToList();

        // Apply sorting
        var endpointSorter = new EndpointSorter(options.SortOptions);
        var findingSorter = new FindingSorter(options.SortOptions);

        var sortedEndpoints = endpointSorter.Sort(filteredEndpoints).ToList();
        var sortedFindings = findingSorter.Sort(filteredFindings).ToList();

        // Apply grouping
        var grouper = new EndpointGrouper(options.GroupOptions);
        var endpointGroups = grouper.GroupEndpoints(sortedEndpoints);
        var findingGroups = grouper.GroupFindings(sortedFindings);

        var output = new JsonOutput
        {
            Summary = new SummaryInfo
            {
                ScannedPath = result.ScannedPath,
                FilesScanned = result.ScannedFiles.Count,
                FailedFiles = result.FailedFiles.Count,
                TotalEndpoints = result.Endpoints.Count,
                FilteredEndpoints = filteredEndpoints.Count,
                TotalFindings = result.Findings.Count,
                FilteredFindings = filteredFindings.Count,
                DurationMs = result.Duration.TotalMilliseconds
            },
            Endpoints = endpointGroups is null
                ? sortedEndpoints.Select(ToEndpointInfo).ToList()
                : null,
            EndpointGroups = endpointGroups?.Select(g => new EndpointGroupInfo
            {
                Key = g.Key,
                DisplayName = g.DisplayName,
                Count = g.Count,
                Endpoints = g.Endpoints.Select(ToEndpointInfo).ToList()
            }).ToList(),
            Findings = findingGroups is null
                ? sortedFindings.Select(ToFindingInfo).ToList()
                : null,
            FindingGroups = findingGroups?.Select(g => new FindingGroupInfo
            {
                Key = g.Key,
                DisplayName = g.DisplayName,
                Count = g.Count,
                Findings = g.Findings.Select(ToFindingInfo).ToList()
            }).ToList(),
            SeverityCounts = result.Findings
                .GroupBy(f => f.Severity)
                .ToDictionary(g => g.Key.ToString(), g => g.Count())
        };

        return JsonSerializer.Serialize(output, Options);
    }

    private static EndpointInfo ToEndpointInfo(Endpoint e)
    {
        return new EndpointInfo
        {
            Route = e.Route,
            Methods = e.MethodsDisplay,
            Type = e.Type.ToString(),
            Classification = e.Classification.ToString(),
            Location = e.Location.ToString(),
            ControllerName = e.ControllerName,
            ActionName = e.ActionName
        };
    }

    private static FindingInfo ToFindingInfo(Finding f)
    {
        return new FindingInfo
        {
            RuleId = f.RuleId,
            RuleName = f.RuleName,
            Severity = f.Severity.ToString(),
            Route = f.Endpoint.Route,
            Location = f.Endpoint.Location.ToString(),
            Message = f.Message,
            Recommendation = f.Recommendation
        };
    }

    private sealed class JsonOutput
    {
        public required SummaryInfo Summary { get; init; }
        public List<EndpointInfo>? Endpoints { get; init; }
        public List<EndpointGroupInfo>? EndpointGroups { get; init; }
        public List<FindingInfo>? Findings { get; init; }
        public List<FindingGroupInfo>? FindingGroups { get; init; }
        public required Dictionary<string, int> SeverityCounts { get; init; }
    }

    private sealed class SummaryInfo
    {
        public required string ScannedPath { get; init; }
        public required int FilesScanned { get; init; }
        public required int FailedFiles { get; init; }
        public required int TotalEndpoints { get; init; }
        public required int FilteredEndpoints { get; init; }
        public required int TotalFindings { get; init; }
        public required int FilteredFindings { get; init; }
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

    private sealed class EndpointGroupInfo
    {
        public required string Key { get; init; }
        public required string DisplayName { get; init; }
        public required int Count { get; init; }
        public required List<EndpointInfo> Endpoints { get; init; }
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

    private sealed class FindingGroupInfo
    {
        public required string Key { get; init; }
        public required string DisplayName { get; init; }
        public required int Count { get; init; }
        public required List<FindingInfo> Findings { get; init; }
    }
}
