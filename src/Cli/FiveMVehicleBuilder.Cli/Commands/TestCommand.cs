using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Cli.Commands;

public class TestCommand : AsyncCommand<TestCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<record>")]
        [Description("Record-ID")]
        public required string RecordId { get; set; }
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
        var buildService = new BuildService(xmlValidationService);

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
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Verfügbare Records:");
            foreach (var r in library.Records.Values.Take(10))
            {
                AnsiConsole.MarkupLine($"  - {r.Id} ({r.OriginalName}) - {r.Variants.Count} Variante(n)");
            }
            if (library.Records.Count > 10)
            {
                AnsiConsole.MarkupLine($"  ... und {library.Records.Count - 10} weitere");
            }
            return 1;
        }

        AnsiConsole.MarkupLine($"[blue]Baue Test-Resource:[/] {settings.RecordId}");
        AnsiConsole.MarkupLine($"Original: {record.OriginalName}");
        AnsiConsole.MarkupLine($"Varianten: {record.Variants.Count}");
        AnsiConsole.WriteLine();

        // Bauen
        BuildResult result = null!;

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("Baue Test-Resource...", maxValue: 100);

                var progress = new Progress<BuildProgress>(p =>
                {
                    task.Description = $"{p.Stage}: {p.CurrentItem}";
                    task.Value = p.Percentage;
                });

                result = await buildService.BuildTestAsync(library, settings.RecordId, progress);
                task.Value = 100;
            });

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Build fehlgeschlagen:[/] {result.ErrorMessage}");
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓ Test-Resource erstellt![/]");
        AnsiConsole.MarkupLine($"  Ausgabe: {result.OutputPath}");
        AnsiConsole.MarkupLine($"  Varianten: {result.Statistics?.VariantCount}");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Kopiere den Ordner in deine FiveM-Server resources/ und starte mit:");
        AnsiConsole.MarkupLine($"  [yellow]ensure test_{settings.RecordId}[/]");

        return 0;
    }
}
