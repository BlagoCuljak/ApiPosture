using System.Text;
using System.Net;
using ApiPosture.Core.Filtering;
using ApiPosture.Core.Grouping;
using ApiPosture.Core.Models;
using ApiPosture.Core.Sorting;

namespace ApiPosture.Output;

/// <summary>
/// Formats scan results as Html file.
/// </summary>
public sealed class HtmlFormatter : IOutputFormatter
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

        sb.AppendLine("""
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>ApiPosture Security Scan Report</title>

    <style>
        :root {
            --bg: #f3f6fb;
            --panel: #ffffff;
            --panel-light: #f8fafc;
            --border: #dbe3ee;

            --text: #1e293b;
            --muted: #64748b;

            --critical: #dc2626;
            --high: #ea580c;
            --medium: #d97706;
            --low: #2563eb;
            --info: #64748b;
            --success: #16a34a;

            --shadow: rgba(15, 23, 42, 0.08);
        }

        * {
            box-sizing: border-box;
        }

        html {
            scroll-behavior: smooth;
        }

        body {
            margin: 0;
            padding: 32px;
            background: linear-gradient(to bottom right, #f8fafc, #eef4fb);
            color: var(--text);
            font-family: Inter, Segoe UI, Arial, sans-serif;
            line-height: 1.6;
        }

        .container {
            max-width: 1500px;
            margin: 0 auto;
        }

        h1 {
            font-size: 42px;
            margin-bottom: 8px;
            color: #0f172a;
        }

        h2 {
            margin-top: 50px;
            margin-bottom: 20px;
            border-bottom: 1px solid var(--border);
            padding-bottom: 12px;
            color: #0f172a;
        }

        h3 {
            margin-top: 0;
            color: #1e293b;
        }

        .meta {
            color: var(--muted);
            margin-bottom: 40px;
        }

        .summary-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
            gap: 18px;
            margin-bottom: 40px;
        }

        .summary-card {
            background: var(--panel);
            border: 1px solid var(--border);
            border-radius: 16px;
            padding: 24px;
            transition: 0.2s ease;
            box-shadow: 0 6px 20px var(--shadow);
        }

        .summary-card:hover {
            transform: translateY(-2px);
        }

        .summary-card .label {
            color: var(--muted);
            font-size: 14px;
        }

        .summary-card .value {
            font-size: 34px;
            font-weight: 700;
            margin-top: 8px;
            color: #0f172a;
        }

        table {
            width: 100%;
            border-collapse: collapse;
            margin-top: 18px;
            margin-bottom: 30px;
            overflow: hidden;
            border-radius: 14px;
            box-shadow: 0 6px 18px var(--shadow);
        }

        th {
            background: #eff6ff;
            color: #1e293b;
            text-align: left;
            padding: 15px;
            font-size: 14px;
            border-bottom: 1px solid var(--border);
        }

        td {
            background: var(--panel);
            border-top: 1px solid var(--border);
            padding: 15px;
            vertical-align: top;
        }

        tr:hover td {
            background: #f8fbff;
        }

        code {
            background: #eef2ff;
            color: #1d4ed8;
            padding: 4px 8px;
            border-radius: 6px;
            font-family: Consolas, monospace;
            font-size: 13px;
        }

        .finding {
            background: var(--panel);
            border: 1px solid var(--border);
            border-left: 6px solid var(--medium);
            border-radius: 16px;
            padding: 24px;
            margin-bottom: 24px;
            transition: 0.2s ease;
            box-shadow: 0 6px 18px var(--shadow);
        }

        .finding:hover {
            transform: translateY(-2px);
        }

        .finding.critical {
            border-left-color: var(--critical);
        }

        .finding.high {
            border-left-color: var(--high);
        }

        .finding.medium {
            border-left-color: var(--medium);
        }

        .finding.low {
            border-left-color: var(--low);
        }

        .severity-badge {
            display: inline-block;
            padding: 5px 12px;
            border-radius: 999px;
            font-size: 12px;
            font-weight: bold;
            text-transform: uppercase;
            margin-bottom: 14px;
        }

        .severity-critical {
            background: #fee2e2;
            color: #b91c1c;
        }

        .severity-high {
            background: #ffedd5;
            color: #c2410c;
        }

        .severity-medium {
            background: #fef3c7;
            color: #b45309;
        }

        .severity-low {
            background: #dbeafe;
            color: #1d4ed8;
        }

        .severity-info {
            background: #e5e7eb;
            color: #4b5563;
        }

        .recommendation {
            margin-top: 20px;
            background: #f8fafc;
            border: 1px solid var(--border);
            border-radius: 12px;
            padding: 18px;
        }

        .recommendation-title {
            color: #2563eb;
            font-weight: bold;
            margin-bottom: 10px;
        }

        .severity-list {
            padding-left: 18px;
        }

        .severity-list li {
            margin-bottom: 8px;
        }

        .success {
            padding: 18px;
            border-radius: 12px;
            background: #dcfce7;
            color: #166534;
            border: 1px solid #86efac;
            font-weight: bold;
        }

        .section-subtitle {
            color: var(--muted);
            margin-bottom: 20px;
        }
    </style>
</head>

<body>
<div class="container">
""");

        // Header
        sb.AppendLine("<h1>🛡️ ApiPosture Security Scan Report</h1>");

        sb.AppendLine($"""
<div class="meta">
    <strong>Generated:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
</div>
""");

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
            var successIndicator = accessibility.GetSuccessIndicator();

            sb.AppendLine("<h2>Security Findings</h2>");
            sb.AppendLine($"""
<div class="success">
    {HtmlEncode(successIndicator)} No security findings detected!
</div>
""");
        }

        sb.AppendLine("""
</div>
</body>
</html>
""");

        return sb.ToString();
    }

    private void RenderSummary(StringBuilder sb, ScanResult result, int filteredEndpoints, int filteredFindings)
    {
        sb.AppendLine("<h2>Summary</h2>");

        sb.AppendLine("""
<div class="summary-grid">
""");

        AppendSummaryCard(sb, "Files Scanned", result.ScannedFiles.Count.ToString());
        AppendSummaryCard(sb, "Failed Files", result.FailedFiles.Count.ToString());
        AppendSummaryCard(sb, "Total Endpoints", result.Endpoints.Count.ToString());
        AppendSummaryCard(sb, "Filtered Endpoints", filteredEndpoints.ToString());
        AppendSummaryCard(sb, "Total Findings", result.Findings.Count.ToString());
        AppendSummaryCard(sb, "Filtered Findings", filteredFindings.ToString());
        AppendSummaryCard(sb, "Scan Duration", $"{result.Duration.TotalMilliseconds:F0}ms");

        sb.AppendLine("</div>");

        sb.AppendLine($"""
<p class="section-subtitle">
    <strong>Scanned Path:</strong>
    <code>{HtmlEncode(result.ScannedPath)}</code>
</p>
""");
    }

    private void RenderSeverityBreakdown(
        StringBuilder sb,
        IReadOnlyList<Finding> findings,
        AccessibilityHelper accessibility)
    {
        var severityCounts = findings
            .GroupBy(f => f.Severity)
            .OrderByDescending(g => g.Key)
            .ToList();

        if (severityCounts.Count == 0)
        {
            return;
        }

        sb.AppendLine("<h2>Severity Breakdown</h2>");
        sb.AppendLine("<ul class=\"severity-list\">");

        foreach (var group in severityCounts)
        {
            var indicator = accessibility.GetSeverityIndicator(group.Key);

            sb.AppendLine($"""
<li>
    {HtmlEncode(indicator)}
    <strong>{HtmlEncode(group.Key.ToString())}</strong>
    — {group.Count()} finding(s)
</li>
""");
        }

        sb.AppendLine("</ul>");
    }

    private void RenderEndpoints(
        StringBuilder sb,
        IEnumerable<Endpoint> endpoints,
        AccessibilityHelper accessibility)
    {
        sb.AppendLine("<h2>Discovered Endpoints</h2>");

        RenderEndpointTable(sb, endpoints, accessibility);
    }

    private void RenderGroupedEndpoints(
        StringBuilder sb,
        IReadOnlyList<EndpointGroup> groups,
        AccessibilityHelper accessibility)
    {
        sb.AppendLine("<h2>Discovered Endpoints</h2>");

        foreach (var group in groups)
        {
            sb.AppendLine(
                $"<h3>{HtmlEncode(group.DisplayName)} ({group.Count} endpoints)</h3>");

            RenderEndpointTable(sb, group.Endpoints, accessibility);
        }
    }

    private void RenderEndpointTable(
        StringBuilder sb,
        IEnumerable<Endpoint> endpoints,
        AccessibilityHelper accessibility)
    {
        sb.AppendLine("""
<table>
    <thead>
        <tr>
            <th>Route</th>
            <th>Methods</th>
            <th>Type</th>
            <th>Classification</th>
        </tr>
    </thead>
    <tbody>
""");

        foreach (var endpoint in endpoints)
        {
            var typeIndicator = accessibility.GetEndpointTypeIndicator(endpoint.Type);
            var classIndicator = accessibility.GetClassificationIndicator(endpoint.Classification);

            sb.AppendLine($"""
<tr>
    <td><code>{HtmlEncode(endpoint.Route)}</code></td>
    <td>{HtmlEncode(endpoint.MethodsDisplay)}</td>
    <td>{HtmlEncode(typeIndicator)} {HtmlEncode(endpoint.Type.ToString())}</td>
    <td>{HtmlEncode(classIndicator)} {HtmlEncode(endpoint.Classification.ToString())}</td>
</tr>
""");
        }

        sb.AppendLine("""
    </tbody>
</table>
""");
    }

    private void RenderFindings(
        StringBuilder sb,
        IEnumerable<Finding> findings,
        AccessibilityHelper accessibility)
    {
        sb.AppendLine("<h2>Security Findings</h2>");

        foreach (var finding in findings)
        {
            RenderFinding(sb, finding, accessibility, "h3");
        }
    }

    private void RenderGroupedFindings(
        StringBuilder sb,
        IReadOnlyList<FindingGroup> groups,
        AccessibilityHelper accessibility)
    {
        sb.AppendLine("<h2>Security Findings</h2>");

        foreach (var group in groups)
        {
            sb.AppendLine($"<h3>{HtmlEncode(group.DisplayName)} ({group.Count})</h3>");

            foreach (var finding in group.Findings)
            {
                RenderFinding(sb, finding, accessibility, "h4");
            }
        }
    }

    private void RenderFinding(
        StringBuilder sb,
        Finding finding,
        AccessibilityHelper accessibility,
        string headerTag)
    {
        var severityIndicator = accessibility.GetSeverityIndicator(finding.Severity);
        var severityClass = finding.Severity.ToString().ToLower();

        sb.AppendLine($"""
<div class="finding {severityClass}">
""");

        sb.AppendLine($"""
<span class="severity-badge severity-{severityClass}">
    {HtmlEncode(severityIndicator)} {HtmlEncode(finding.Severity.ToString())}
</span>
""");

        sb.AppendLine(
            $"<{headerTag}>[{HtmlEncode(finding.RuleId)}] {HtmlEncode(finding.RuleName)}</{headerTag}>");

        sb.AppendLine(
            $"<p><strong>Route:</strong> <code>{HtmlEncode(finding.Endpoint.Route)}</code></p>");

        sb.AppendLine(
            $"<p><strong>Location:</strong> <code>{HtmlEncode(finding.Endpoint.Location.ToString())}</code></p>");

        sb.AppendLine($"<p>{HtmlEncode(finding.Message)}</p>");

        sb.AppendLine($"""
<div class="recommendation">
    <div class="recommendation-title">Recommendation</div>
    <div>{HtmlEncode(finding.Recommendation)}</div>
</div>
""");

        sb.AppendLine("</div>");
    }

    private static void AppendSummaryCard(StringBuilder sb, string label, string value)
    {
        sb.AppendLine($"""
<div class="summary-card">
    <div class="label">{HtmlEncode(label)}</div>
    <div class="value">{HtmlEncode(value)}</div>
</div>
""");
    }

    private static string HtmlEncode(string? value)
    {
        return WebUtility.HtmlEncode(value ?? string.Empty);
    }
}