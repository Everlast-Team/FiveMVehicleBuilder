using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Cli.Commands;

public class BuildCommand : AsyncCommand<BuildCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<pack>")]
        [Description("Pack-Name")]
        public required string PackName { get; set; }

        [CommandOption("--skip-check")]
        [Description("Prüfungen überspringen")]
        public bool SkipCheck { get; set; }
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
        var buildService = new BuildService(xmlValidationService);

        if (!libraryService.IsValidLibrary(appSettings.LibraryPath))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Bibliothek nicht gefunden: {appSettings.LibraryPath}");
            return 1;
        }

        var library = await libraryService.OpenAsync(appSettings.LibraryPath);

        // Pack prüfen
        if (!library.Packs.TryGetValue(settings.PackName, out var pack))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Pack '{settings.PackName}' nicht gefunden.");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Verfügbare Packs:");
            foreach (var p in library.Packs.Values)
            {
                var status = p.Active ? "[green]aktiv[/]" : "[dim]inaktiv[/]";
                AnsiConsole.MarkupLine($"  - {p.Name} ({p.Records.Count} Records) {status}");
            }
            return 1;
        }

        AnsiConsole.MarkupLine($"[blue]Baue Pack:[/] {pack.Name} -> {pack.ResourceName}");
        AnsiConsole.MarkupLine($"Records: {pack.Records.Count}");
        AnsiConsole.WriteLine();

        // Prüfungen
        if (!settings.SkipCheck)
        {
            AnsiConsole.MarkupLine("Führe Prüfungen durch...");
            var checkResult = await checkService.CheckAllAsync(library);

            if (checkResult.HasErrors)
            {
                AnsiConsole.MarkupLine($"[red]Prüfung fehlgeschlagen:[/] {checkResult.ErrorCount} Fehler");
                foreach (var error in checkResult.Findings.Where(f => f.Severity == CheckSeverity.Error))
                {
                    AnsiConsole.MarkupLine($"  [red]✗[/] {error.Message}");
                }
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("Nutze [yellow]--skip-check[/] um trotzdem zu bauen (nicht empfohlen).");
                return 1;
            }

            if (checkResult.WarningCount > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]{checkResult.WarningCount} Warnung(en)[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[green]✓[/] Prüfung bestanden");
            }
            AnsiConsole.WriteLine();
        }

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
                var task = ctx.AddTask("Baue Varianten...", maxValue: 100);

                var progress = new Progress<BuildProgress>(p =>
                {
                    task.Description = $"{p.Stage}: {p.CurrentItem}";
                    task.Value = p.Percentage;
                });

                result = await buildService.BuildPackAsync(library, settings.PackName, progress);
                task.Value = 100;
            });

        if (!result.Success)
        {
            AnsiConsole.MarkupLine($"[red]Build fehlgeschlagen:[/] {result.ErrorMessage}");

            // Bei XML-Validierungsfehlern: Details anzeigen
            if (result.CheckResult?.HasErrors == true)
            {
                AnsiConsole.WriteLine();
                foreach (var error in result.CheckResult.Findings.Where(f => f.Severity == CheckSeverity.Error))
                {
                    AnsiConsole.MarkupLine($"  [red]✗[/] {error.Message}");
                    if (!string.IsNullOrEmpty(error.FilePath))
                    {
                        AnsiConsole.MarkupLine($"    Datei: {error.FilePath}");
                    }
                }
            }
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[green]✓ Build erfolgreich![/]");
        AnsiConsole.MarkupLine($"  Ausgabe: {result.OutputPath}");
        AnsiConsole.MarkupLine($"  Records: {result.Statistics?.RecordCount}");
        AnsiConsole.MarkupLine($"  Varianten: {result.Statistics?.VariantCount}");
        AnsiConsole.MarkupLine($"  Größe: {result.Statistics?.TotalSizeBytes / 1024.0:F1} KB");
        AnsiConsole.MarkupLine($"  Dauer: {result.Statistics?.Duration.TotalSeconds:F2}s");

        return 0;
    }
}
