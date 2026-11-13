using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Cli.Commands;

public class ImportCommand : AsyncCommand<ImportCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Pfad zur FiveM-Resource")]
        public required string Path { get; set; }

        [CommandOption("-c|--category")]
        [Description("Kategorie (z.B. einsatz, zivil)")]
        public string Category { get; set; } = "einsatz";

        [CommandOption("-s|--source")]
        [Description("Quellen-Beschreibung")]
        public string? Source { get; set; }
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
        var importService = new ImportService();

        if (!libraryService.IsValidLibrary(appSettings.LibraryPath))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Bibliothek nicht gefunden: {appSettings.LibraryPath}");
            return 1;
        }

        var library = await libraryService.OpenAsync(appSettings.LibraryPath);

        // Resource analysieren
        AnsiConsole.MarkupLine($"[blue]Analysiere:[/] {settings.Path}");
        var analysis = await importService.AnalyzeAsync(settings.Path);

        // Warnungen anzeigen
        foreach (var warning in analysis.Warnings)
        {
            AnsiConsole.MarkupLine($"[yellow]Warnung:[/] {warning}");
        }

        // Übersprungene Fahrzeuge anzeigen
        foreach (var skipped in analysis.SkippedVehicles)
        {
            AnsiConsole.MarkupLine($"[yellow]Übersprungen:[/] {skipped.ModelName} - {skipped.Reason}");
        }

        // Verwaiste Dateien anzeigen
        if (analysis.OrphanedFiles.Count > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]Nicht zugeordnete Dateien:[/] {analysis.OrphanedFiles.Count}");
        }

        if (analysis.Vehicles.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Keine importierbaren Fahrzeuge gefunden.[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Gefunden:[/] {analysis.Vehicles.Count} Fahrzeug(e)");
        AnsiConsole.WriteLine();

        // Tabelle mit gefundenen Fahrzeugen
        var table = new Table();
        table.AddColumn("Modell");
        table.AddColumn("Handling-ID");
        table.AddColumn("Siren-ID");
        table.AddColumn("Dateien");

        foreach (var vehicle in analysis.Vehicles)
        {
            var files = new List<string>();
            if (vehicle.StreamFiles.YftPath != null) files.Add(".yft");
            if (vehicle.StreamFiles.HiYftPath != null) files.Add("_hi.yft");
            if (vehicle.StreamFiles.YtdPath != null) files.Add(".ytd");

            table.AddRow(
                vehicle.ModelName,
                vehicle.HandlingId ?? "-",
                vehicle.SirenSettingId?.ToString() ?? "-",
                string.Join(", ", files));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Importieren
        var importedCount = 0;
        foreach (var vehicle in analysis.Vehicles)
        {
            try
            {
                var record = await importService.ImportAsync(library, vehicle, settings.Category, settings.Source);
                AnsiConsole.MarkupLine($"[green]✓[/] {vehicle.ModelName} -> {record.Id} ({record.Variants[0].Name})");
                importedCount++;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] {vehicle.ModelName}: {ex.Message}");
            }
        }

        // Bibliothek speichern
        await libraryService.SaveAsync(library);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]{importedCount} Fahrzeug(e) importiert.[/]");

        return 0;
    }
}
