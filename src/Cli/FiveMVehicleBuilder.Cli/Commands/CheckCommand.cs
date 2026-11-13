using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Cli.Commands;

public class CheckCommand : AsyncCommand<CheckCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[record]")]
        [Description("Record-ID zum Prüfen (leer = alle)")]
        public string? RecordId { get; set; }

        [CommandOption("--all")]
        [Description("Alle Records prüfen")]
        public bool All { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // Bibliothek laden
        var appSettings = AppSettings.Load();
        if (string.IsNullOrEmpty(appSettings.LibraryPath))
        {
            AnsiConsole.MarkupLine("[red]Fehler:[/] Keine Bibliothek konfiguriert. Nutze [yellow]fvb init[/] zuerst.");
            return 1;
        }

        var libraryService = new LibraryService();
        var xmlValidationService = new XmlValidationService();
        var checkService = new CheckService(xmlValidationService);

        if (!libraryService.IsValidLibrary(appSettings.LibraryPath))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Bibliothek nicht gefunden: {appSettings.LibraryPath}");
            return 1;
        }

        var library = await libraryService.OpenAsync(appSettings.LibraryPath);

        CheckResult result;

        if (settings.All || string.IsNullOrEmpty(settings.RecordId))
        {
            AnsiConsole.MarkupLine("[blue]Prüfe alle Records...[/]");
            result = await checkService.CheckAllAsync(library);
        }
        else
        {
            AnsiConsole.MarkupLine($"[blue]Prüfe Record:[/] {settings.RecordId}");
            result = await checkService.CheckRecordAsync(library, settings.RecordId);
        }

        // Ergebnisse anzeigen
        AnsiConsole.WriteLine();

        if (result.Findings.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]✓ Keine Probleme gefunden.[/]");
            return 0;
        }

        // Fehler zuerst
        var errors = result.Findings.Where(f => f.Severity == CheckSeverity.Error).ToList();
        var warnings = result.Findings.Where(f => f.Severity == CheckSeverity.Warning).ToList();

        if (errors.Count > 0)
        {
            AnsiConsole.MarkupLine($"[red]Fehler: {errors.Count}[/]");
            foreach (var error in errors)
            {
                var location = FormatLocation(error);
                AnsiConsole.MarkupLine($"  [red]✗[/] [{error.Code}] {error.Message}{location}");
            }
            AnsiConsole.WriteLine();
        }

        if (warnings.Count > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Warnungen: {warnings.Count}[/]");
            foreach (var warning in warnings)
            {
                var location = FormatLocation(warning);
                AnsiConsole.MarkupLine($"  [yellow]![/] [{warning.Code}] {warning.Message}{location}");
            }
            AnsiConsole.WriteLine();
        }

        // Zusammenfassung
        if (result.HasErrors)
        {
            AnsiConsole.MarkupLine("[red]Build würde fehlschlagen.[/]");
            return 1;
        }
        else
        {
            AnsiConsole.MarkupLine("[green]Build möglich (mit Warnungen).[/]");
            return 0;
        }
    }

    private static string FormatLocation(CheckFinding finding)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(finding.RecordId))
            parts.Add($"Record: {finding.RecordId}");

        if (!string.IsNullOrEmpty(finding.VariantName))
            parts.Add($"Variante: {finding.VariantName}");

        if (parts.Count == 0)
            return "";

        return $" ({string.Join(", ", parts)})";
    }
}
