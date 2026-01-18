using ApiPosture.Core.Filtering;
using ApiPosture.Core.Grouping;
using ApiPosture.Core.Models;
using ApiPosture.Core.Sorting;
using Spectre.Console;

namespace ApiPosture.Output;

/// <summary>
/// Formats scan results for terminal output using Spectre.Console.
/// </summary>
public sealed class TerminalFormatter : IOutputFormatter
{
    public string Format(ScanResult result, OutputOptions options)
    {
        // This method returns empty string because we write directly to console
        // Use RenderToConsole instead for terminal output
        return string.Empty;
    }

    public void RenderToConsole(ScanResult result, OutputOptions options)
    {
        var console = AnsiConsole.Console;
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
        console.WriteLine();
        if (options.UseColors)
        {
            console.Write(new Rule("[bold blue]ApiPosture Security Scan[/]").RuleStyle("blue"));
        }
        else
        {
            console.Write(new Rule("ApiPosture Security Scan"));
        }
        console.WriteLine();

        // Findings details FIRST (so they appear at top when scrolling up)
        if (sortedFindings.Count > 0)
        {
            if (findingGroups is not null)
            {
                RenderGroupedFindings(console, findingGroups, options);
            }
            else
            {
                RenderFindings(console, sortedFindings, options);
            }
        }

        // Separator with scroll hint if there are findings
        if (sortedFindings.Count > 0)
        {
            console.WriteLine();
            var scrollHint = options.UseColors
                ? "[dim]^^^^ Scroll up for finding details ^^^^[/]"
                : "^^^^ Scroll up for finding details ^^^^";
            console.Write(new Rule(scrollHint).RuleStyle("grey"));
            console.WriteLine();
        }

        // Summary panel
        RenderSummary(console, result, filteredEndpoints.Count, filteredFindings.Count, options);

        // Severity summary chart (compact overview)
        RenderSeveritySummary(console, result.Findings, options);

        // Endpoints table at BOTTOM (visible immediately after scan)
        if (sortedEndpoints.Count > 0)
        {
            console.WriteLine();
            if (endpointGroups is not null)
            {
                RenderGroupedEndpoints(console, endpointGroups, options);
            }
            else
            {
                RenderEndpoints(console, sortedEndpoints, options);
            }
        }

        // Final status
        if (sortedFindings.Count == 0)
        {
            if (options.UseColors)
            {
                console.MarkupLine("[green]No security findings![/]");
            }
            else
            {
                console.WriteLine("No security findings!");
            }
        }

        console.WriteLine();
    }

    private void RenderSummary(IAnsiConsole console, ScanResult result, int filteredEndpoints, int filteredFindings, OutputOptions options)
    {
        var summaryTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Metric")
            .AddColumn("Value");

        summaryTable.AddRow("Scanned Path", result.ScannedPath);
        summaryTable.AddRow("Files Scanned", result.ScannedFiles.Count.ToString());
        summaryTable.AddRow("Failed Files", result.FailedFiles.Count.ToString());
        summaryTable.AddRow("Total Endpoints", result.Endpoints.Count.ToString());
        summaryTable.AddRow("Filtered Endpoints", filteredEndpoints.ToString());
        summaryTable.AddRow("Total Findings", result.Findings.Count.ToString());
        summaryTable.AddRow("Filtered Findings", filteredFindings.ToString());
        summaryTable.AddRow("Scan Duration", $"{result.Duration.TotalMilliseconds:F0}ms");

        console.Write(summaryTable);
        console.WriteLine();
    }

    private void RenderEndpoints(IAnsiConsole console, IEnumerable<Endpoint> endpoints, OutputOptions options)
    {
        console.Write(new Rule(options.UseColors ? "[bold]Discovered Endpoints[/]" : "Discovered Endpoints").LeftJustified());

        var endpointsTable = CreateEndpointsTable();

        foreach (var endpoint in endpoints)
        {
            AddEndpointRow(endpointsTable, endpoint, options);
        }

        console.Write(endpointsTable);
        console.WriteLine();
    }

    private void RenderGroupedEndpoints(IAnsiConsole console, IReadOnlyList<EndpointGroup> groups, OutputOptions options)
    {
        foreach (var group in groups)
        {
            var header = $"{group.DisplayName} ({group.Count} endpoints)";
            console.Write(new Rule(options.UseColors ? $"[bold]{Markup.Escape(header)}[/]" : header).LeftJustified());

            var endpointsTable = CreateEndpointsTable();

            foreach (var endpoint in group.Endpoints)
            {
                AddEndpointRow(endpointsTable, endpoint, options);
            }

            console.Write(endpointsTable);
            console.WriteLine();
        }
    }

    private Table CreateEndpointsTable()
    {
        return new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Route")
            .AddColumn("Methods")
            .AddColumn("Type")
            .AddColumn("Classification");
    }

    private void AddEndpointRow(Table table, Endpoint endpoint, OutputOptions options)
    {
        var typeIndicator = options.Accessibility.GetEndpointTypeIndicator(endpoint.Type);
        var classIndicator = options.Accessibility.GetClassificationIndicator(endpoint.Classification);

        string classificationDisplay;
        string typeDisplay;

        if (options.UseColors)
        {
            var classColor = options.Accessibility.GetClassificationColor(endpoint.Classification);
            classificationDisplay = $"[{classColor}]{classIndicator} {endpoint.Classification}[/]";
            typeDisplay = $"{typeIndicator} {endpoint.Type}";
        }
        else
        {
            // Escape brackets in text indicators for table cells
            classificationDisplay = Markup.Escape($"{classIndicator} {endpoint.Classification}");
            typeDisplay = Markup.Escape($"{typeIndicator} {endpoint.Type}");
        }

        table.AddRow(
            Markup.Escape(endpoint.Route),
            endpoint.MethodsDisplay,
            typeDisplay,
            classificationDisplay);
    }

    private void RenderFindings(IAnsiConsole console, IEnumerable<Finding> findings, OutputOptions options)
    {
        var findingsList = findings.ToList();
        console.Write(new Rule(options.UseColors
            ? $"[bold]Security Findings ({findingsList.Count})[/]"
            : $"Security Findings ({findingsList.Count})").LeftJustified());

        foreach (var finding in findingsList)
        {
            RenderFinding(console, finding, options);
        }
    }

    private void RenderGroupedFindings(IAnsiConsole console, IReadOnlyList<FindingGroup> groups, OutputOptions options)
    {
        foreach (var group in groups)
        {
            var header = $"{group.DisplayName} ({group.Count})";
            console.Write(new Rule(options.UseColors ? $"[bold]{Markup.Escape(header)}[/]" : header).LeftJustified());

            foreach (var finding in group.Findings)
            {
                RenderFinding(console, finding, options);
            }

            console.WriteLine();
        }
    }

    private void RenderFinding(IAnsiConsole console, Finding finding, OutputOptions options)
    {
        var severityIndicator = options.Accessibility.GetSeverityIndicator(finding.Severity);
        var escapedRuleName = Markup.Escape(finding.RuleName);

        string headerText;
        if (options.UseColors)
        {
            var severityColor = options.Accessibility.GetSeverityColor(finding.Severity);
            headerText = $"[{severityColor}]{severityIndicator} [[{finding.RuleId}]] {escapedRuleName} ({finding.Severity})[/]";
        }
        else
        {
            // PanelHeader still parses markup, so we need to escape all brackets
            var escapedIndicator = Markup.Escape(severityIndicator);
            headerText = $"{escapedIndicator} [[{finding.RuleId}]] {escapedRuleName} ({finding.Severity})";
        }

        var content = options.UseColors
            ? $"""
               [bold]Route:[/] {Markup.Escape(finding.Endpoint.Route)}
               [bold]Location:[/] {finding.Endpoint.Location}

               {Markup.Escape(finding.Message)}

               [dim]Recommendation:[/] {Markup.Escape(finding.Recommendation)}
               """
            : $"""
               Route: {finding.Endpoint.Route}
               Location: {finding.Endpoint.Location}

               {finding.Message}

               Recommendation: {finding.Recommendation}
               """;

        var panel = new Panel(options.UseColors ? new Markup(content) : new Text(content))
        {
            Header = new PanelHeader(headerText),
            Border = BoxBorder.Rounded
        };

        console.Write(panel);
    }

    private void RenderSeveritySummary(IAnsiConsole console, IReadOnlyList<Finding> findings, OutputOptions options)
    {
        console.WriteLine();

        var severityCounts = findings
            .GroupBy(f => f.Severity)
            .OrderByDescending(g => g.Key)
            .ToList();

        if (severityCounts.Count > 0)
        {
            if (options.UseColors)
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
            else
            {
                // Text-based summary when colors are disabled
                console.WriteLine("Severity Breakdown:");
                foreach (var group in severityCounts)
                {
                    var indicator = options.Accessibility.GetSeverityIndicator(group.Key);
                    var escapedIndicator = Markup.Escape(indicator);
                    console.WriteLine($"  {escapedIndicator} {group.Key}: {group.Count()}");
                }
            }
        }
    }
}
