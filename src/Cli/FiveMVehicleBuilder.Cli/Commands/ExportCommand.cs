using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Cli.Commands;

public class ExportCommand : AsyncCommand<ExportCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<pack>")]
        [Description("Pack-Name")]
        public required string PackName { get; set; }

        [CommandOption("-f|--format")]
        [Description("Export-Format (json, lua, sql)")]
        [DefaultValue("json")]
        public string Format { get; set; } = "json";

        [CommandOption("-o|--output")]
        [Description("Ausgabepfad")]
        public string? OutputPath { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        var appSettings = AppSettings.Load();
        if (string.IsNullOrEmpty(appSettings.LibraryPath))
        {
            AnsiConsole.MarkupLine("[red]Fehler:[/] Keine Bibliothek konfiguriert. Nutze [yellow]fvb init[/] zuerst.");
            return 1;
        }

        var libraryService = new LibraryService();
        var exportService = new ExportService();

        if (!libraryService.IsValidLibrary(appSettings.LibraryPath))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Bibliothek nicht gefunden: {appSettings.LibraryPath}");
            return 1;
        }

        var library = await libraryService.OpenAsync(appSettings.LibraryPath);

        // Pack prüfen
        if (!library.Packs.ContainsKey(settings.PackName))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Pack '{settings.PackName}' nicht gefunden.");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Verfügbare Packs:");
            foreach (var p in library.Packs.Values)
            {
                AnsiConsole.MarkupLine($"  - {p.Name}");
            }
            return 1;
        }

        // Format parsen
        var format = settings.Format.ToLowerInvariant() switch
        {
            "json" => ExportFormat.Json,
            "lua" => ExportFormat.Lua,
            "sql" => ExportFormat.Sql,
            _ => ExportFormat.Json
        };

        AnsiConsole.MarkupLine($"[blue]Exportiere Pack:[/] {settings.PackName} ({format})");

        var result = await exportService.ExportAsync(library, settings.PackName, format, settings.OutputPath);

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Export fehlgeschlagen:[/] {result.ErrorMessage}");
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓ Export erfolgreich![/]");
        AnsiConsole.MarkupLine($"  Fahrzeuge: {result.VehicleCount}");
        AnsiConsole.MarkupLine($"  Ausgabe: {result.OutputPath}");

        return 0;
    }
}
