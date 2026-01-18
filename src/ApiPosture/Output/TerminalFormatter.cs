using ApiPosture.Core.Models;
using Spectre.Console;

namespace ApiPosture.Output;

/// <summary>
/// Formats scan results for terminal output using Spectre.Console.
/// </summary>
public sealed class TerminalFormatter : IOutputFormatter
{
    public string Format(ScanResult result, Severity minSeverity)
    {
        // This method returns empty string because we write directly to console
        // Use RenderToConsole instead for terminal output
        return string.Empty;
    }

    public void RenderToConsole(ScanResult result, Severity minSeverity)
    {
        var console = AnsiConsole.Console;

        // Header
        console.WriteLine();
        console.Write(new Rule("[bold blue]ApiPosture Security Scan[/]").RuleStyle("blue"));
        console.WriteLine();

        // Summary panel
        var summaryTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value");

        summaryTable.AddRow("Scanned Path", result.ScannedPath);
        summaryTable.AddRow("Files Scanned", result.ScannedFiles.Count.ToString());
        summaryTable.AddRow("Failed Files", result.FailedFiles.Count.ToString());
        summaryTable.AddRow("Endpoints Found", result.Endpoints.Count.ToString());
        summaryTable.AddRow("Total Findings", result.Findings.Count.ToString());
        summaryTable.AddRow("Scan Duration", $"{result.Duration.TotalMilliseconds:F0}ms");

        console.Write(summaryTable);
        console.WriteLine();

        // Endpoints table
        if (result.Endpoints.Count > 0)
        {
            console.Write(new Rule("[bold]Discovered Endpoints[/]").LeftJustified());

            var endpointsTable = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Route")
                .AddColumn("Methods")
                .AddColumn("Type")
                .AddColumn("Classification");

            foreach (var endpoint in result.Endpoints.OrderBy(e => e.Route))
            {
                var classColor = endpoint.Classification switch
                {
                    SecurityClassification.Public => "red",
                    SecurityClassification.Authenticated => "yellow",
                    SecurityClassification.RoleRestricted => "green",
                    SecurityClassification.PolicyRestricted => "green",
                    _ => "white"
                };

                endpointsTable.AddRow(
                    endpoint.Route,
                    endpoint.MethodsDisplay,
                    endpoint.Type.ToString(),
                    $"[{classColor}]{endpoint.Classification}[/]");
            }

            console.Write(endpointsTable);
            console.WriteLine();
        }

        // Findings
        var filteredFindings = result.GetFindingsBySeverity(minSeverity);

        if (filteredFindings.Count > 0)
        {
            console.Write(new Rule($"[bold]Security Findings ({filteredFindings.Count})[/]").LeftJustified());

            foreach (var finding in filteredFindings.OrderByDescending(f => f.Severity))
            {
                var severityColor = finding.Severity switch
                {
                    Severity.Critical => "red bold",
                    Severity.High => "red",
                    Severity.Medium => "yellow",
                    Severity.Low => "blue",
                    Severity.Info => "grey",
                    _ => "white"
                };

                var panel = new Panel(new Markup($"""
                    [bold]Route:[/] {Markup.Escape(finding.Endpoint.Route)}
                    [bold]Location:[/] {finding.Endpoint.Location}

                    {Markup.Escape(finding.Message)}

                    [dim]Recommendation:[/] {Markup.Escape(finding.Recommendation)}
                    """))
                {
                    Header = new PanelHeader($"[{severityColor}][[{finding.RuleId}]] {finding.RuleName} ({finding.Severity})[/]"),
                    Border = BoxBorder.Rounded
                };

                console.Write(panel);
            }
        }
        else
        {
            console.MarkupLine("[green]No security findings![/]");
        }

        // Severity summary
        console.WriteLine();
        var severityCounts = result.Findings
            .GroupBy(f => f.Severity)
            .OrderByDescending(g => g.Key)
            .ToList();

        if (severityCounts.Count > 0)
        {
            var chart = new BreakdownChart()
                .Width(60);

            foreach (var group in severityCounts)
            {
                var color = group.Key switch
                {
                    Severity.Critical => Color.Red,
                    Severity.High => Color.OrangeRed1,
                    Severity.Medium => Color.Yellow,
                    Severity.Low => Color.Blue,
                    Severity.Info => Color.Grey,
                    _ => Color.White
                };

                chart.AddItem(group.Key.ToString(), group.Count(), color);
            }

            console.Write(chart);
        }

        console.WriteLine();
    }
}
