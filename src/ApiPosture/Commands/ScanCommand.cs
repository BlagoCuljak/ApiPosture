using System.ComponentModel;
using System.Diagnostics;
using ApiPosture.Core.Analysis;
using ApiPosture.Core.Models;
using ApiPosture.Output;
using ApiPosture.Rules;
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

        [Description("Output format (terminal, json, markdown)")]
        [CommandOption("-o|--output")]
        [DefaultValue("terminal")]
        public string Output { get; init; } = "terminal";

        [Description("Output file path")]
        [CommandOption("-f|--output-file")]
        public string? OutputFile { get; init; }

        [Description("Minimum severity to report (info, low, medium, high, critical)")]
        [CommandOption("--severity")]
        [DefaultValue("info")]
        public string Severity { get; init; } = "info";

        [Description("Fail with exit code 1 if findings at or above this severity (info, low, medium, high, critical)")]
        [CommandOption("--fail-on")]
        public string? FailOn { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        var path = settings.Path ?? Directory.GetCurrentDirectory();

        if (!Directory.Exists(path) && !File.Exists(path))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Path not found: {path}");
            return 1;
        }

        // Parse severity options
        if (!TryParseSeverity(settings.Severity, out var minSeverity))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Invalid severity: {settings.Severity}");
            return 1;
        }

        Severity? failOnSeverity = null;
        if (settings.FailOn != null)
        {
            if (!TryParseSeverity(settings.FailOn, out var parsedFailOn))
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Invalid fail-on severity: {settings.FailOn}");
                return 1;
            }
            failOnSeverity = parsedFailOn;
        }

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
                var ruleEngine = new RuleEngine();
                var findings = ruleEngine.Evaluate(analysisResult.Endpoints);

                stopwatch.Stop();

                return new ScanResult
                {
                    ScannedPath = System.IO.Path.GetFullPath(path),
                    Endpoints = analysisResult.Endpoints,
                    Findings = findings,
                    ScannedFiles = analysisResult.ScannedFiles,
                    FailedFiles = analysisResult.FailedFiles,
                    Duration = stopwatch.Elapsed
                };
            });

        // Format output
        var output = settings.Output.ToLowerInvariant();
        string? formattedOutput = null;

        switch (output)
        {
            case "terminal":
                var terminalFormatter = new TerminalFormatter();
                terminalFormatter.RenderToConsole(result, minSeverity);
                break;

            case "json":
                var jsonFormatter = new JsonFormatter();
                formattedOutput = jsonFormatter.Format(result, minSeverity);
                break;

            case "markdown" or "md":
                var markdownFormatter = new MarkdownFormatter();
                formattedOutput = markdownFormatter.Format(result, minSeverity);
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
