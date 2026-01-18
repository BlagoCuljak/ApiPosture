using System.Text;
using ApiPosture.Core.Models;

namespace ApiPosture.Output;

/// <summary>
/// Formats scan results as Markdown.
/// </summary>
public sealed class MarkdownFormatter : IOutputFormatter
{
    public string Format(ScanResult result, Severity minSeverity)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# ApiPosture Security Scan Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        // Summary
        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Scanned Path | `{result.ScannedPath}` |");
        sb.AppendLine($"| Files Scanned | {result.ScannedFiles.Count} |");
        sb.AppendLine($"| Failed Files | {result.FailedFiles.Count} |");
        sb.AppendLine($"| Endpoints Found | {result.Endpoints.Count} |");
        sb.AppendLine($"| Total Findings | {result.Findings.Count} |");
        sb.AppendLine($"| Scan Duration | {result.Duration.TotalMilliseconds:F0}ms |");
        sb.AppendLine();

        // Severity breakdown
        var severityCounts = result.Findings
            .GroupBy(f => f.Severity)
            .OrderByDescending(g => g.Key)
            .ToList();

        if (severityCounts.Count > 0)
        {
            sb.AppendLine("### Findings by Severity");
            sb.AppendLine();
            foreach (var group in severityCounts)
            {
                var emoji = group.Key switch
                {
                    Severity.Critical => "🔴",
                    Severity.High => "🟠",
                    Severity.Medium => "🟡",
                    Severity.Low => "🔵",
                    Severity.Info => "⚪",
                    _ => "⚪"
                };
                sb.AppendLine($"- {emoji} **{group.Key}:** {group.Count()}");
            }
            sb.AppendLine();
        }

        // Endpoints table
        if (result.Endpoints.Count > 0)
        {
            sb.AppendLine("## Discovered Endpoints");
            sb.AppendLine();
            sb.AppendLine("| Route | Methods | Type | Classification |");
            sb.AppendLine("|-------|---------|------|----------------|");

            foreach (var endpoint in result.Endpoints.OrderBy(e => e.Route))
            {
                sb.AppendLine($"| `{endpoint.Route}` | {endpoint.MethodsDisplay} | {endpoint.Type} | {endpoint.Classification} |");
            }
            sb.AppendLine();
        }

        // Findings
        var filteredFindings = result.GetFindingsBySeverity(minSeverity);

        if (filteredFindings.Count > 0)
        {
            sb.AppendLine("## Security Findings");
            sb.AppendLine();

            foreach (var finding in filteredFindings.OrderByDescending(f => f.Severity))
            {
                var severityBadge = finding.Severity switch
                {
                    Severity.Critical => "🔴 Critical",
                    Severity.High => "🟠 High",
                    Severity.Medium => "🟡 Medium",
                    Severity.Low => "🔵 Low",
                    Severity.Info => "⚪ Info",
                    _ => finding.Severity.ToString()
                };

                sb.AppendLine($"### [{finding.RuleId}] {finding.RuleName}");
                sb.AppendLine();
                sb.AppendLine($"**Severity:** {severityBadge}");
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
        else
        {
            sb.AppendLine("## Security Findings");
            sb.AppendLine();
            sb.AppendLine("✅ No security findings detected!");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
