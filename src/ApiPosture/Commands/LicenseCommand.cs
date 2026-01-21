using System.ComponentModel;
using ApiPosture.Core.Licensing;
using ApiPosture.Extensions;
using ApiPosture.Licensing;
using Spectre.Console;
using Spectre.Console.Cli;

namespace ApiPosture.Commands;

/// <summary>
/// License management commands.
/// </summary>
public sealed class LicenseCommand : Command<LicenseCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
    }

    public override int Execute(CommandContext context, Settings settings)
    {
        AnsiConsole.MarkupLine("[yellow]Usage:[/] apiposture license <command>");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Commands:");
        AnsiConsole.MarkupLine("  [blue]activate[/] <key>   Activate a license key");
        AnsiConsole.MarkupLine("  [blue]deactivate[/]       Deactivate the current license");
        AnsiConsole.MarkupLine("  [blue]status[/]           Show license status");
        return 0;
    }
}

/// <summary>
/// Activates a license key.
/// </summary>
public sealed class LicenseActivateCommand : AsyncCommand<LicenseActivateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("The license key to activate")]
        [CommandArgument(0, "<key>")]
        public required string Key { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var licenseManager = new Licensing.LicenseManager(ExtensionLoader.DataDirectory);

        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Activating license...", async ctx =>
            {
                return await licenseManager.ActivateAsync(settings.Key);
            });

        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]Success:[/] {result.Message}");
            AnsiConsole.WriteLine();

            if (result.License != null)
            {
                RenderLicenseInfo(result.License);
            }

            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {result.ErrorMessage}");
            return 1;
        }
    }

    private static void RenderLicenseInfo(ILicenseContext license)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Property")
            .AddColumn("Value");

        table.AddRow("Tier", $"[blue]{license.Tier}[/]");

        if (license.Organization != null)
        {
            table.AddRow("Organization", license.Organization);
        }

        table.AddRow("Seats", license.Seats.ToString());

        if (license.ExpiresAt.HasValue)
        {
            var expiry = license.ExpiresAt.Value;
            var daysRemaining = (expiry - DateTimeOffset.UtcNow).TotalDays;
            var expiryColor = daysRemaining < 30 ? "yellow" : "green";
            table.AddRow("Expires", $"[{expiryColor}]{expiry:yyyy-MM-dd}[/] ({(int)daysRemaining} days)");
        }
        else
        {
            table.AddRow("Expires", "[green]Never[/]");
        }

        if (license.IsCiLicense)
        {
            table.AddRow("Type", "[cyan]CI/CD License[/]");
        }

        table.AddRow("Features", string.Join(", ", license.Features));

        AnsiConsole.Write(table);
    }
}

/// <summary>
/// Deactivates the current license.
/// </summary>
public sealed class LicenseDeactivateCommand : AsyncCommand<LicenseDeactivateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        // Confirm deactivation
        if (!AnsiConsole.Confirm("Are you sure you want to deactivate your license?", false))
        {
            AnsiConsole.MarkupLine("[yellow]Cancelled.[/]");
            return 0;
        }

        using var licenseManager = new Licensing.LicenseManager(ExtensionLoader.DataDirectory);

        var result = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Deactivating license...", async ctx =>
            {
                return await licenseManager.DeactivateAsync();
            });

        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]Success:[/] {result.Message}");
            return 0;
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {result.ErrorMessage}");
            return 1;
        }
    }
}

/// <summary>
/// Shows license status.
/// </summary>
public sealed class LicenseStatusCommand : AsyncCommand<LicenseStatusCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        using var licenseManager = new Licensing.LicenseManager(ExtensionLoader.DataDirectory);

        var status = await licenseManager.GetStatusAsync();
        var license = status.License;

        AnsiConsole.WriteLine();

        if (license.Tier == LicenseTier.Free)
        {
            AnsiConsole.MarkupLine("[yellow]No Pro license active[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("You are using the free tier with core features only.");
            AnsiConsole.MarkupLine("Upgrade to Pro for advanced features: [blue]https://apiposture.com/pricing[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("To activate a license:");
            AnsiConsole.MarkupLine("  [dim]apiposture license activate <your-license-key>[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("For CI/CD pipelines, set the environment variable:");
            AnsiConsole.MarkupLine("  [dim]APIPOSTURE_LICENSE_KEY=<your-license-key>[/]");
            return 0;
        }

        // Display license info
        var panel = new Panel(BuildLicenseMarkup(license, status))
            .Header("[blue][[ApiPosture Pro License]][/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);

        // Show features
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[blue]Licensed Features:[/]");
        foreach (var feature in license.Features)
        {
            var featureName = feature switch
            {
                LicenseFeatures.DiffMode => "Diff Mode - Compare scans",
                LicenseFeatures.HistoricalTracking => "Historical Tracking - Store and trend scans",
                LicenseFeatures.RiskScoring => "Risk Scoring - Numeric risk assessment",
                LicenseFeatures.AdvancedOwaspRules => "Advanced OWASP Rules (AP101-AP108)",
                LicenseFeatures.SecretsScanning => "Secrets Scanning - Detect hardcoded credentials",
                _ => feature
            };
            AnsiConsole.MarkupLine($"  [green]+[/] {featureName}");
        }

        return 0;
    }

    private static string BuildLicenseMarkup(ILicenseContext license, LicenseStatus status)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine($"[blue]Tier:[/] {license.Tier}");

        if (license.Organization != null)
        {
            sb.AppendLine($"[blue]Organization:[/] {license.Organization}");
        }

        sb.AppendLine($"[blue]Seats:[/] {license.Seats}");

        if (license.ExpiresAt.HasValue)
        {
            var expiry = license.ExpiresAt.Value;
            var daysRemaining = (int)(expiry - DateTimeOffset.UtcNow).TotalDays;
            var expiryColor = daysRemaining < 30 ? "yellow" : "green";
            sb.AppendLine($"[blue]Expires:[/] [{expiryColor}]{expiry:yyyy-MM-dd}[/] ({daysRemaining} days remaining)");
        }
        else
        {
            sb.AppendLine("[blue]Expires:[/] [green]Never[/] (perpetual license)");
        }

        if (license.IsCiLicense)
        {
            sb.AppendLine("[blue]Type:[/] [cyan]CI/CD License[/] (no machine binding)");
        }

        if (status.IsFromEnvironment)
        {
            sb.AppendLine("[blue]Source:[/] [cyan]Environment variable[/] (APIPOSTURE_LICENSE_KEY)");
        }
        else if (status.ActivatedAt.HasValue)
        {
            sb.AppendLine($"[blue]Activated:[/] {status.ActivatedAt.Value:yyyy-MM-dd HH:mm}");
            if (status.LastValidatedAt.HasValue)
            {
                sb.AppendLine($"[blue]Last Validated:[/] {status.LastValidatedAt.Value:yyyy-MM-dd HH:mm}");
            }
        }

        return sb.ToString().TrimEnd();
    }
}
