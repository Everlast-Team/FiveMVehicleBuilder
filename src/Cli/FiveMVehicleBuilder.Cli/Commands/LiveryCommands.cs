using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Cli.Commands;

public class LiveryAssignCommand : AsyncCommand<LiveryAssignCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<variant>")]
        [Description("Varianten-Spawnname")]
        public required string VariantName { get; set; }

        [CommandArgument(1, "<index>")]
        [Description("Livery-Index (1-basiert)")]
        public int LiveryIndex { get; set; }

        [CommandArgument(2, "<department>")]
        [Description("Department-Kürzel (z.B. SAPD, SAMS, VFD)")]
        public required string Department { get; set; }
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

        if (!libraryService.IsValidLibrary(appSettings.LibraryPath))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Bibliothek nicht gefunden: {appSettings.LibraryPath}");
            return 1;
        }

        var library = await libraryService.OpenAsync(appSettings.LibraryPath);

        // Variante finden
        Variant? targetVariant = null;
        Record? targetRecord = null;

        foreach (var record in library.Records.Values)
        {
            var variant = record.Variants.FirstOrDefault(v =>
                v.Name.Equals(settings.VariantName, StringComparison.OrdinalIgnoreCase));

            if (variant != null)
            {
                targetVariant = variant;
                targetRecord = record;
                break;
            }
        }

        if (targetVariant == null || targetRecord == null)
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Variante '{settings.VariantName}' nicht gefunden.");
            return 1;
        }

        // Livery-Index validieren
        if (settings.LiveryIndex < 1)
        {
            AnsiConsole.MarkupLine("[red]Fehler:[/] Livery-Index muss >= 1 sein.");
            return 1;
        }

        // Prüfen ob bereits zugeordnet
        var existingAssignment = targetVariant.Liveries
            .FirstOrDefault(l => l.Index == settings.LiveryIndex);

        if (existingAssignment != null)
        {
            existingAssignment.Department = settings.Department.ToUpperInvariant();
            AnsiConsole.MarkupLine($"[green]✓[/] Livery {settings.LiveryIndex} aktualisiert: {settings.Department.ToUpperInvariant()}");
        }
        else
        {
            targetVariant.Liveries.Add(new LiveryAssignment
            {
                Index = settings.LiveryIndex,
                Department = settings.Department.ToUpperInvariant()
            });
            AnsiConsole.MarkupLine($"[green]✓[/] Livery {settings.LiveryIndex} zugeordnet: {settings.Department.ToUpperInvariant()}");
        }

        await libraryService.SaveAsync(library);

        return 0;
    }
}

public class LiveryRemoveCommand : AsyncCommand<LiveryRemoveCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<variant>")]
        [Description("Varianten-Spawnname")]
        public required string VariantName { get; set; }

        [CommandArgument(1, "<index>")]
        [Description("Livery-Index (1-basiert)")]
        public int LiveryIndex { get; set; }
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

        if (!libraryService.IsValidLibrary(appSettings.LibraryPath))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Bibliothek nicht gefunden: {appSettings.LibraryPath}");
            return 1;
        }

        var library = await libraryService.OpenAsync(appSettings.LibraryPath);

        // Variante finden
        Variant? targetVariant = null;

        foreach (var record in library.Records.Values)
        {
            var variant = record.Variants.FirstOrDefault(v =>
                v.Name.Equals(settings.VariantName, StringComparison.OrdinalIgnoreCase));

            if (variant != null)
            {
                targetVariant = variant;
                break;
            }
        }

        if (targetVariant == null)
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Variante '{settings.VariantName}' nicht gefunden.");
            return 1;
        }

        var assignment = targetVariant.Liveries
            .FirstOrDefault(l => l.Index == settings.LiveryIndex);

        if (assignment == null)
        {
            AnsiConsole.MarkupLine($"[yellow]Hinweis:[/] Livery {settings.LiveryIndex} hat keine Zuordnung.");
            return 0;
        }

        targetVariant.Liveries.Remove(assignment);
        await libraryService.SaveAsync(library);

        AnsiConsole.MarkupLine($"[green]✓[/] Livery {settings.LiveryIndex} Zuordnung entfernt.");

        return 0;
    }
}

public class LiveryListCommand : AsyncCommand<LiveryListCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[variant]")]
        [Description("Varianten-Spawnname (optional, sonst alle)")]
        public string? VariantName { get; set; }

        [CommandOption("-d|--department")]
        [Description("Nach Department filtern")]
        public string? Department { get; set; }
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

        if (!libraryService.IsValidLibrary(appSettings.LibraryPath))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Bibliothek nicht gefunden: {appSettings.LibraryPath}");
            return 1;
        }

        var library = await libraryService.OpenAsync(appSettings.LibraryPath);

        var table = new Table();
        table.Border(TableBorder.Simple);
        table.AddColumn("Variante");
        table.AddColumn("Livery");
        table.AddColumn("Department");

        var hasEntries = false;

        foreach (var record in library.Records.Values.OrderBy(r => r.Id))
        {
            foreach (var variant in record.Variants.OrderBy(v => v.Name))
            {
                // Filter nach Variante
                if (!string.IsNullOrEmpty(settings.VariantName) &&
                    !variant.Name.Equals(settings.VariantName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var livery in variant.Liveries.OrderBy(l => l.Index))
                {
                    // Filter nach Department
                    if (!string.IsNullOrEmpty(settings.Department) &&
                        !livery.Department.Equals(settings.Department, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    table.AddRow(variant.Name, livery.Index.ToString(), livery.Department);
                    hasEntries = true;
                }
            }
        }

        if (!hasEntries)
        {
            if (!string.IsNullOrEmpty(settings.VariantName))
            {
                AnsiConsole.MarkupLine($"[dim]Keine Livery-Zuordnungen für '{settings.VariantName}'.[/]");
            }
            else if (!string.IsNullOrEmpty(settings.Department))
            {
                AnsiConsole.MarkupLine($"[dim]Keine Liveries für Department '{settings.Department}'.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[dim]Keine Livery-Zuordnungen vorhanden.[/]");
            }
            return 0;
        }

        AnsiConsole.Write(table);

        return 0;
    }
}
