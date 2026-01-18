using System.Text;
using ApiPosture.Core.Filtering;
using ApiPosture.Core.Grouping;
using ApiPosture.Core.Models;
using ApiPosture.Core.Sorting;

namespace ApiPosture.Output;

/// <summary>
/// Formats scan results as Markdown.
/// </summary>
public sealed class MarkdownFormatter : IOutputFormatter
{
    public string Format(ScanResult result, OutputOptions options)
    {
        var sb = new StringBuilder();
        var accessibility = options.Accessibility;

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

        // Header
        sb.AppendLine("# ApiPosture Security Scan Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Summary
        RenderSummary(sb, result, filteredEndpoints.Count, filteredFindings.Count);

        // Severity breakdown
        RenderSeverityBreakdown(sb, result.Findings, accessibility);

        // Endpoints
        if (sortedEndpoints.Count > 0)
        {
            if (endpointGroups is not null)
            {
                RenderGroupedEndpoints(sb, endpointGroups, accessibility);
            }
            else
            {
                RenderEndpoints(sb, sortedEndpoints, accessibility);
            }
        }

        // Findings
        if (sortedFindings.Count > 0)
        {
            if (findingGroups is not null)
            {
                RenderGroupedFindings(sb, findingGroups, accessibility);
            }
            else
            {
                RenderFindings(sb, sortedFindings, accessibility);
            }
        }
        else
        {
            sb.AppendLine("## Security Findings");
            sb.AppendLine();
            var successIndicator = accessibility.GetSuccessIndicator();
            sb.AppendLine($"{successIndicator} No security findings detected!");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private void RenderSummary(StringBuilder sb, ScanResult result, int filteredEndpoints, int filteredFindings)
    {
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Scanned Path | `{result.ScannedPath}` |");
        sb.AppendLine($"| Files Scanned | {result.ScannedFiles.Count} |");
        sb.AppendLine($"| Failed Files | {result.FailedFiles.Count} |");
        sb.AppendLine($"| Total Endpoints | {result.Endpoints.Count} |");
        sb.AppendLine($"| Filtered Endpoints | {filteredEndpoints} |");
        sb.AppendLine($"| Total Findings | {result.Findings.Count} |");
        sb.AppendLine($"| Filtered Findings | {filteredFindings} |");
        sb.AppendLine($"| Scan Duration | {result.Duration.TotalMilliseconds:F0}ms |");
        sb.AppendLine();
    }

    private void RenderSeverityBreakdown(StringBuilder sb, IReadOnlyList<Finding> findings, AccessibilityHelper accessibility)
    {
        var severityCounts = findings
            .GroupBy(f => f.Severity)
            .OrderByDescending(g => g.Key)
            .ToList();

        if (severityCounts.Count > 0)
        {
            sb.AppendLine("### Findings by Severity");
            sb.AppendLine();
            foreach (var group in severityCounts)
            {
                var indicator = accessibility.GetSeverityIndicator(group.Key);
                sb.AppendLine($"- {indicator} **{group.Key}:** {group.Count()}");
            }
            sb.AppendLine();
        }
    }

    private void RenderEndpoints(StringBuilder sb, IEnumerable<Endpoint> endpoints, AccessibilityHelper accessibility)
    {
        sb.AppendLine("## Discovered Endpoints");
        sb.AppendLine();
        sb.AppendLine("| Route | Methods | Type | Classification |");
        sb.AppendLine("|-------|---------|------|----------------|");

        foreach (var endpoint in endpoints)
        {
            var typeIndicator = accessibility.GetEndpointTypeIndicator(endpoint.Type);
            var classIndicator = accessibility.GetClassificationIndicator(endpoint.Classification);
            sb.AppendLine($"| `{endpoint.Route}` | {endpoint.MethodsDisplay} | {typeIndicator} {endpoint.Type} | {classIndicator} {endpoint.Classification} |");
        }
        sb.AppendLine();
    }

    private void RenderGroupedEndpoints(StringBuilder sb, IReadOnlyList<EndpointGroup> groups, AccessibilityHelper accessibility)
    {
        sb.AppendLine("## Discovered Endpoints");
        sb.AppendLine();

        foreach (var group in groups)
        {
            sb.AppendLine($"### {group.DisplayName} ({group.Count} endpoints)");
            sb.AppendLine();
            sb.AppendLine("| Route | Methods | Type | Classification |");
            sb.AppendLine("|-------|---------|------|----------------|");

            foreach (var endpoint in group.Endpoints)
            {
                var typeIndicator = accessibility.GetEndpointTypeIndicator(endpoint.Type);
                var classIndicator = accessibility.GetClassificationIndicator(endpoint.Classification);
                sb.AppendLine($"| `{endpoint.Route}` | {endpoint.MethodsDisplay} | {typeIndicator} {endpoint.Type} | {classIndicator} {endpoint.Classification} |");
            }
            sb.AppendLine();
        }
    }

    private void RenderFindings(StringBuilder sb, IEnumerable<Finding> findings, AccessibilityHelper accessibility)
    {
        sb.AppendLine("## Security Findings");
        sb.AppendLine();

        foreach (var finding in findings)
        {
            RenderFinding(sb, finding, accessibility);
        }
    }

    private void RenderGroupedFindings(StringBuilder sb, IReadOnlyList<FindingGroup> groups, AccessibilityHelper accessibility)
    {
        sb.AppendLine("## Security Findings");
        sb.AppendLine();

        foreach (var group in groups)
        {
            sb.AppendLine($"### {group.DisplayName} ({group.Count})");
            sb.AppendLine();

            foreach (var finding in group.Findings)
            {
                RenderFinding(sb, finding, accessibility, headerLevel: 4);
            }
        }
    }

    private void RenderFinding(StringBuilder sb, Finding finding, AccessibilityHelper accessibility, int headerLevel = 3)
    {
        var header = new string('#', headerLevel);
        var severityIndicator = accessibility.GetSeverityIndicator(finding.Severity);

        sb.AppendLine($"{header} [{finding.RuleId}] {finding.RuleName}");
        sb.AppendLine();
        sb.AppendLine($"**Severity:** {severityIndicator} {finding.Severity}");
        sb.AppendLine();
        sb.AppendLine($"**Route:** `{finding.Endpoint.Route}`");
        sb.AppendLine();
        sb.AppendLine($"**Location:** `{finding.Endpoint.Location}`");
        sb.AppendLine();
        sb.AppendLine(finding.Message);
        sb.AppendLine();
        sb.AppendLine($"> **Recommendation:** {finding.Recommendation}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }
}
