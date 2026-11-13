using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Cli.Commands;

public class InitCommand : AsyncCommand<InitCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "[path]")]
        [Description("Pfad zur Bibliothek (optional, wird interaktiv abgefragt)")]
        public string? Path { get; set; }

        [CommandOption("--no-git")]
        [Description("Git-Repository nicht initialisieren")]
        public bool NoGit { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[blue]FiveM Vehicle Builder[/] - Initialisierung");
        AnsiConsole.WriteLine();

        var libraryService = new LibraryService();

        // Git-Installation prüfen
        if (!settings.NoGit)
        {
            var gitCheck = await libraryService.CheckGitInstallationAsync();
            if (!gitCheck.IsReady)
            {
                AnsiConsole.MarkupLine("[red]Fehler:[/] Git oder Git LFS ist nicht installiert.");
                if (!gitCheck.GitInstalled)
                    AnsiConsole.MarkupLine("  - Git: [red]nicht gefunden[/]");
                else
                    AnsiConsole.MarkupLine($"  - Git: [green]{gitCheck.GitVersion}[/]");

                if (!gitCheck.GitLfsInstalled)
                    AnsiConsole.MarkupLine("  - Git LFS: [red]nicht gefunden[/]");
                else
                    AnsiConsole.MarkupLine($"  - Git LFS: [green]{gitCheck.GitLfsVersion}[/]");

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("Bitte installiere Git und Git LFS oder nutze [yellow]--no-git[/]");
                return 1;
            }

            AnsiConsole.MarkupLine($"[green]✓[/] Git: {gitCheck.GitVersion}");
            AnsiConsole.MarkupLine($"[green]✓[/] Git LFS: {gitCheck.GitLfsVersion}");
            AnsiConsole.WriteLine();
        }

        // Pfad ermitteln
        var path = settings.Path;
        if (string.IsNullOrEmpty(path))
        {
            path = AnsiConsole.Ask<string>("Bibliothekspfad:");
        }

        path = System.IO.Path.GetFullPath(path);

        // Prüfen ob bereits eine Bibliothek existiert
        if (libraryService.IsValidLibrary(path))
        {
            AnsiConsole.MarkupLine($"[yellow]Bibliothek existiert bereits:[/] {path}");

            // Als aktive Bibliothek setzen
            var appSettings = AppSettings.Load();
            appSettings.LibraryPath = path;
            appSettings.Save();

            AnsiConsole.MarkupLine("[green]✓[/] Als aktive Bibliothek gesetzt");
            return 0;
        }

        // Neue Bibliothek erstellen
        try
        {
            await AnsiConsole.Status()
                .StartAsync("Erstelle Bibliothek...", async ctx =>
                {
                    await libraryService.CreateAsync(path, initGit: !settings.NoGit);
                });

            // Als aktive Bibliothek setzen
            var appSettings = AppSettings.Load();
            appSettings.LibraryPath = path;
            appSettings.Save();

            AnsiConsole.MarkupLine($"[green]✓[/] Bibliothek erstellt: {path}");
            if (!settings.NoGit)
            {
                AnsiConsole.MarkupLine("[green]✓[/] Git-Repository initialisiert");
                AnsiConsole.MarkupLine("[green]✓[/] Git LFS für .yft/.ytd konfiguriert");
            }

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] {ex.Message}");
            return 1;
        }
    }
}
