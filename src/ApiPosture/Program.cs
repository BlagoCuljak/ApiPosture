using ApiPosture.Commands;
using ApiPosture.Services;
using Spectre.Console;
using Spectre.Console.Cli;

// Initialize services (license manager, extension loader)
ServiceLocator.Initialize();

// Report any extension loading errors (non-fatal)
var loadResult = ServiceLocator.ExtensionLoadResult;
foreach (var error in loadResult.Errors)
{
    if (error.MissingFeatures != null && error.MissingFeatures.Count > 0)
    {
        // License feature errors are informational, not warnings
        // (user may not have Pro license and that's fine)
    }
    else
    {
        AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(error.Message)}");
    }
}

var app = new CommandApp<ScanCommand>();

app.Configure(config =>
{
    config.SetApplicationName("apiposture");
    config.SetApplicationVersion("1.0.0");

    // Core scan command
    config.AddCommand<ScanCommand>("scan")
        .WithDescription("Scan an ASP.NET Core API project for security issues")
        .WithExample("scan", ".")
        .WithExample("scan", "./src/MyApi")
        .WithExample("scan", ".", "--output", "json")
        .WithExample("scan", ".", "--output", "markdown", "--output-file", "report.md")
        .WithExample("scan", ".", "--fail-on", "high");

    // Note: Extension commands are registered through the extension system.
    // Commands from extensions are registered at startup when extensions are loaded.
    // Due to Spectre.Console.Cli's design, dynamic command registration requires
    // extensions to provide command types that can be registered via reflection.
    // For now, extension commands must be registered in the extension's Initialize method
    // by calling back into a registration API.

    // Display loaded extensions info if any
    if (ServiceLocator.ExtensionLoadResult.Extensions.Count > 0)
    {
        // Extensions are loaded - their commands will be available through the extension's own registration
    }
});

var result = app.Run(args);

// Cleanup
ServiceLocator.Dispose();

return result;
