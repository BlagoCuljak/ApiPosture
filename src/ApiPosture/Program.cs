using ApiPosture.Commands;
using Spectre.Console.Cli;

var app = new CommandApp<ScanCommand>();

app.Configure(config =>
{
    config.SetApplicationName("apiposture");
    config.SetApplicationVersion("1.0.0");

    config.AddCommand<ScanCommand>("scan")
        .WithDescription("Scan an ASP.NET Core API project for security issues")
        .WithExample("scan", ".")
        .WithExample("scan", "./src/MyApi")
        .WithExample("scan", ".", "--output", "json")
        .WithExample("scan", ".", "--output", "markdown", "--output-file", "report.md")
        .WithExample("scan", ".", "--fail-on", "high");
});

return app.Run(args);
