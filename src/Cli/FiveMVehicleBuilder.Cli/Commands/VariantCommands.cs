using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Cli.Commands;

public class VariantAddCommand : AsyncCommand<VariantAddCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<record>")]
        [Description("Record-ID")]
        public required string RecordId { get; set; }

        [CommandArgument(1, "[suffix]")]
        [Description("Suffix für den Spawnnamen (Standard: nächster Buchstabe)")]
        public string? Suffix { get; set; }

        [CommandOption("--siren")]
        [Description("Siren-Setting-ID")]
        public int? SirenSettingId { get; set; }
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

        // Record prüfen
        if (!library.Records.TryGetValue(settings.RecordId, out var record))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Record '{settings.RecordId}' nicht gefunden.");
            return 1;
        }

        // Suffix bestimmen (nächster Buchstabe falls nicht angegeben)
        var suffix = settings.Suffix;
        if (string.IsNullOrEmpty(suffix))
        {
            // Nächsten freien Buchstaben finden
            var usedSuffixes = record.Variants
                .Select(v => v.Name.Replace(settings.RecordId, ""))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            for (var c = 'a'; c <= 'z'; c++)
            {
                if (!usedSuffixes.Contains(c.ToString()))
                {
                    suffix = c.ToString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(suffix))
            {
                AnsiConsole.MarkupLine("[red]Fehler:[/] Keine freien Suffixe mehr verfügbar (a-z).");
                return 1;
            }
        }

        // Spawnname generieren
        var spawnName = $"{settings.RecordId}{suffix}";

        // Prüfen ob Variante bereits existiert
        if (record.Variants.Any(v => v.Name.Equals(spawnName, StringComparison.OrdinalIgnoreCase)))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Variante '{spawnName}' existiert bereits.");
            return 1;
        }

        // Neue Variante erstellen
        var newVariant = new Variant
        {
            Name = spawnName,
            SirenSettingId = settings.SirenSettingId
        };

        record.Variants.Add(newVariant);
        await libraryService.SaveAsync(library);

        AnsiConsole.MarkupLine($"[green]✓[/] Variante '{spawnName}' zu Record '{settings.RecordId}' hinzugefügt.");
        if (settings.SirenSettingId.HasValue)
        {
            AnsiConsole.MarkupLine($"  Siren-Setting: {settings.SirenSettingId}");
        }

        return 0;
    }
}

public class VariantListCommand : AsyncCommand<VariantListCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<record>")]
        [Description("Record-ID")]
        public required string RecordId { get; set; }
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

        // Record prüfen
        if (!library.Records.TryGetValue(settings.RecordId, out var record))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Record '{settings.RecordId}' nicht gefunden.");
            return 1;
        }

        AnsiConsole.MarkupLine($"[blue]Record:[/] {settings.RecordId} ({record.OriginalName})");
        AnsiConsole.MarkupLine($"[dim]Kategorie:[/] {record.Category}");
        AnsiConsole.WriteLine();

        if (record.Variants.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]Keine Varianten vorhanden.[/]");
            return 0;
        }

        var table = new Table();
        table.Border(TableBorder.Simple);
        table.AddColumn("Spawn-Name");
        table.AddColumn("Siren-ID");
        table.AddColumn("Liveries");

        foreach (var variant in record.Variants.OrderBy(v => v.Name))
        {
            var liveryCount = variant.Liveries.Count;
            table.AddRow(
                variant.Name,
                variant.SirenSettingId?.ToString() ?? "-",
                liveryCount > 0 ? liveryCount.ToString() : "-");
        }

        AnsiConsole.Write(table);

        return 0;
    }
}

public class VariantRemoveCommand : AsyncCommand<VariantRemoveCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<variant>")]
        [Description("Varianten-Spawnname (z.B. ev_suv01b)")]
        public required string VariantName { get; set; }
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

        // Variante in Records finden
        Record? targetRecord = null;
        Variant? targetVariant = null;

        foreach (var record in library.Records.Values)
        {
            var variant = record.Variants.FirstOrDefault(v =>
                v.Name.Equals(settings.VariantName, StringComparison.OrdinalIgnoreCase));

            if (variant != null)
            {
                targetRecord = record;
                targetVariant = variant;
                break;
            }
        }

        if (targetRecord == null || targetVariant == null)
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Variante '{settings.VariantName}' nicht gefunden.");
            return 1;
        }

        // Prüfen ob es die einzige Variante ist
        if (targetRecord.Variants.Count == 1)
        {
            AnsiConsole.MarkupLine("[red]Fehler:[/] Ein Record muss mindestens eine Variante haben.");
            AnsiConsole.MarkupLine("Lösche den gesamten Record stattdessen.");
            return 1;
        }

        targetRecord.Variants.Remove(targetVariant);
        await libraryService.SaveAsync(library);

        AnsiConsole.MarkupLine($"[green]✓[/] Variante '{settings.VariantName}' aus Record '{targetRecord.Id}' entfernt.");

        return 0;
    }
}
