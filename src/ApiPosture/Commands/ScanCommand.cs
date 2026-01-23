using System.ComponentModel;
using System.Diagnostics;
using ApiPosture.Core.Analysis;
using ApiPosture.Core.Configuration;
using ApiPosture.Core.Filtering;
using ApiPosture.Core.Grouping;
using ApiPosture.Core.Models;
using ApiPosture.Core.Sorting;
using ApiPosture.Output;
using ApiPosture.Rules;
using ApiPosture.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ApiPosture.Commands;

/// <summary>
/// Scan command for analyzing ASP.NET Core API projects.
/// </summary>
public sealed class ScanCommand : Command<ScanCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to the project or directory to scan")]
        [CommandArgument(0, "[path]")]
        public string? Path { get; init; }

        // Output options
        [Description("Output format (terminal, json, markdown)")]
        [CommandOption("-o|--output")]
        [DefaultValue("terminal")]
        public string Output { get; init; } = "terminal";

        [Description("Output file path")]
        [CommandOption("-f|--output-file")]
        public string? OutputFile { get; init; }

        // Configuration
        [Description("Path to configuration file (.apiposture.json)")]
        [CommandOption("-c|--config")]
        public string? ConfigFile { get; init; }

        // Severity options
        [Description("Minimum severity to report (info, low, medium, high, critical)")]
        [CommandOption("--severity")]
        [DefaultValue("info")]
        public string Severity { get; init; } = "info";

        [Description("Fail with exit code 1 if findings at or above this severity")]
        [CommandOption("--fail-on")]
        public string? FailOn { get; init; }

        // Sorting options
        [Description("Sort by field (severity, route, method, classification, controller, location)")]
        [CommandOption("--sort-by")]
        public string? SortBy { get; init; }

        [Description("Sort direction (asc, desc)")]
        [CommandOption("--sort-dir")]
        [DefaultValue("desc")]
        public string SortDirection { get; init; } = "desc";

        // Filtering options
        [Description("Filter by security classification (public, authenticated, role-restricted, policy-restricted)")]
        [CommandOption("--classification")]
        public string[]? Classification { get; init; }

        [Description("Filter by HTTP method (GET, POST, PUT, DELETE, PATCH)")]
        [CommandOption("--method")]
        public string[]? Methods { get; init; }

        [Description("Filter routes containing substring (case-insensitive)")]
        [CommandOption("--route-contains")]
        public string? RouteContains { get; init; }

        [Description("Filter by controller name")]
        [CommandOption("--controller")]
        public string[]? Controllers { get; init; }

        [Description("Filter by API style (controller, minimal)")]
        [CommandOption("--api-style")]
        public string[]? ApiStyle { get; init; }

        [Description("Filter by rule ID (AP001, AP002, etc.)")]
        [CommandOption("--rule")]
        public string[]? Rules { get; init; }

        // Grouping options
        [Description("Group endpoints by field (controller, classification, severity, method)")]
        [CommandOption("--group-by")]
        public string? GroupBy { get; init; }

        [Description("Group findings by field (severity, controller, classification)")]
        [CommandOption("--group-findings-by")]
        public string? GroupFindingsBy { get; init; }

        // Accessibility options
        [Description("Disable colored output")]
        [CommandOption("--no-color")]
        public bool NoColor { get; init; }

        [Description("Disable icons/emojis in output")]
        [CommandOption("--no-icons")]
        public bool NoIcons { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var path = settings.Path ?? Directory.GetCurrentDirectory();

        if (!Directory.Exists(path) && !File.Exists(path))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Path not found: {path}");
            return 1;
        }

        // Load configuration
        var configLoader = new ConfigurationLoader();
        var config = configLoader.Load(
            settings.ConfigFile,
            Directory.Exists(path) ? path : System.IO.Path.GetDirectoryName(path),
            Directory.GetCurrentDirectory());

        // Parse severity options (CLI overrides config)
        var severityString = settings.Severity;
        if (string.IsNullOrEmpty(severityString) || severityString == "info")
        {
            // Use config default if available
            severityString = config.Severity?.Default ?? "info";
        }

        if (!TryParseSeverity(severityString, out var minSeverity))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid severity: {severityString}");
            return 1;
        }

        Severity? failOnSeverity = null;
        var failOnString = settings.FailOn ?? config.Severity?.FailOn;
        if (!string.IsNullOrEmpty(failOnString))
        {
            if (!TryParseSeverity(failOnString, out var parsedFailOn))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid fail-on severity: {failOnString}");
                return 1;
            }
            failOnSeverity = parsedFailOn;
        }

        // Build rule engine config with extension rules
        var ruleEngineConfig = new RuleEngineConfig
        {
            SensitiveKeywords = config.GetSensitiveKeywords(),
            ExtensionRules = ServiceLocator.ExtensionRules
        };

        // Perform scan
        var result = AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Scanning...", ctx =>
            {
                var stopwatch = Stopwatch.StartNew();

                ctx.Status("Discovering source files...");
                var analyzer = new ProjectAnalyzer();
                var analysisResult = analyzer.Analyze(path);

                ctx.Status("Evaluating security rules...");
                var ruleEngine = new RuleEngine(ruleEngineConfig);
                var findings = ruleEngine.Evaluate(analysisResult.Endpoints);

                // Apply suppressions from config
                var suppressionMatcher = SuppressionMatcher.FromConfig(config);
                var filteredFindings = suppressionMatcher.FilterSuppressions(findings).ToList();

                stopwatch.Stop();

                return new ScanResult
                {
                    ScannedPath = System.IO.Path.GetFullPath(path),
                    Endpoints = analysisResult.Endpoints,
                    Findings = filteredFindings,
                    ScannedFiles = analysisResult.ScannedFiles,
                    FailedFiles = analysisResult.FailedFiles,
                    Duration = stopwatch.Elapsed
                };
            });

        // Build output options
        var sortOptions = SortOptions.FromStrings(settings.SortBy, settings.SortDirection);
        var filterOptions = FilterOptions.FromStrings(
            settings.Severity,
            settings.Classification,
            settings.Methods,
            settings.RouteContains,
            settings.Controllers,
            settings.ApiStyle,
            settings.Rules);
        var groupOptions = GroupOptions.FromStrings(settings.GroupBy, settings.GroupFindingsBy);
        var accessibility = AccessibilityHelper.Create(
            settings.NoColor,
            settings.NoIcons,
            config.Display?.UseColors,
            config.Display?.UseIcons);

        var outputOptions = OutputOptions.Create(
            minSeverity,
            sortOptions,
            filterOptions,
            groupOptions,
            accessibility);

        // Format output
        var output = settings.Output.ToLowerInvariant();
        string? formattedOutput = null;

        switch (output)
        {
            case "terminal":
                var terminalFormatter = new TerminalFormatter();
                terminalFormatter.RenderToConsole(result, outputOptions);
                break;

            case "json":
                var jsonFormatter = new JsonFormatter();
                formattedOutput = jsonFormatter.Format(result, outputOptions);
                break;

            case "markdown" or "md":
                var markdownFormatter = new MarkdownFormatter();
                formattedOutput = markdownFormatter.Format(result, outputOptions);
                break;

            default:
                AnsiConsole.MarkupLine($"[red]Error:[/] Unknown output format: {output}");
                return 1;
        }

        // Write to file or stdout
        if (formattedOutput != null)
        {
            if (!string.IsNullOrEmpty(settings.OutputFile))
            {
                File.WriteAllText(settings.OutputFile, formattedOutput);
                AnsiConsole.MarkupLine($"[green]Report written to:[/] {settings.OutputFile}");
            }
            else
            {
                Console.WriteLine(formattedOutput);
            }
        }

        // Check fail-on condition
        if (failOnSeverity.HasValue)
        {
            var failingFindings = result.GetFindingsBySeverity(failOnSeverity.Value);
            if (failingFindings.Count > 0)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Found {failingFindings.Count} finding(s) at or above {failOnSeverity.Value} severity.[/]");
                return 1;
            }
        }

        return 0;
    }

    private static bool TryParseSeverity(string value, out Severity severity)
    {
        return Enum.TryParse(value, ignoreCase: true, out severity);
    }
}
