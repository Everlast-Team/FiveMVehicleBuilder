using Spectre.Console;
using Spectre.Console.Cli;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Cli.Commands;

public class ListCommand : AsyncCommand<ListCommand.Settings>
{
    public class Settings : CommandSettings { }

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

        if (!libraryService.IsValidLibrary(appSettings.LibraryPath))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Bibliothek nicht gefunden: {appSettings.LibraryPath}");
            return 1;
        }

        var library = await libraryService.OpenAsync(appSettings.LibraryPath);

        AnsiConsole.MarkupLine($"[blue]Bibliothek:[/] {appSettings.LibraryPath}");
        AnsiConsole.WriteLine();

        if (library.Records.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]Keine Records vorhanden.[/]");
            return 0;
        }

        // Records nach Kategorie gruppieren
        var byCategory = library.Records.Values
            .GroupBy(r => r.Category)
            .OrderBy(g => g.Key);

        foreach (var group in byCategory)
        {
            AnsiConsole.MarkupLine($"[yellow]{group.Key}[/] ({group.Count()} Records)");

            var table = new Table();
            table.Border(TableBorder.Simple);
            table.AddColumn("ID");
            table.AddColumn("Original");
            table.AddColumn("Varianten");
            table.AddColumn("Pack");

            foreach (var record in group.OrderBy(r => r.Id))
            {
                var variants = string.Join(", ", record.Variants.Select(v => v.Name));
                var pack = library.Packs.Values
                    .FirstOrDefault(p => p.Records.Contains(record.Id))?.Name ?? "[dim]-[/]";

                table.AddRow(
                    record.Id,
                    record.OriginalName,
                    variants,
                    pack);
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        // Zusammenfassung
        var totalVariants = library.Records.Values.Sum(r => r.Variants.Count);
        AnsiConsole.MarkupLine($"[green]Gesamt:[/] {library.Records.Count} Records, {totalVariants} Varianten");

        return 0;
    }
}
