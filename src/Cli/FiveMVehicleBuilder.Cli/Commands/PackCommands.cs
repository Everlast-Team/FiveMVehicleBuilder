using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Cli.Commands;

public class PackCreateCommand : AsyncCommand<PackCreateCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<name>")]
        [Description("Name des Packs")]
        public required string Name { get; set; }

        [CommandOption("-r|--resource")]
        [Description("Resource-Name (Standard: vehicles_<name>)")]
        public string? ResourceName { get; set; }
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

        // Prüfen ob Pack bereits existiert
        if (library.Packs.ContainsKey(settings.Name))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Pack '{settings.Name}' existiert bereits.");
            return 1;
        }

        var resourceName = settings.ResourceName ?? $"vehicles_{settings.Name}";

        var pack = new Pack
        {
            Name = settings.Name,
            ResourceName = resourceName,
            Active = true,
            Records = []
        };

        library.Packs[settings.Name] = pack;
        await libraryService.SaveAsync(library);

        AnsiConsole.MarkupLine($"[green]✓[/] Pack erstellt: {settings.Name} -> {resourceName}");

        return 0;
    }
}

public class PackAddCommand : AsyncCommand<PackAddCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<pack>")]
        [Description("Pack-Name")]
        public required string PackName { get; set; }

        [CommandArgument(1, "<record>")]
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

        // Pack prüfen
        if (!library.Packs.TryGetValue(settings.PackName, out var pack))
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

        // Record prüfen
        if (!library.Records.ContainsKey(settings.RecordId))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Record '{settings.RecordId}' nicht gefunden.");
            return 1;
        }

        // Prüfen ob bereits im Pack
        if (pack.Records.Contains(settings.RecordId))
        {
            AnsiConsole.MarkupLine($"[yellow]Hinweis:[/] Record '{settings.RecordId}' ist bereits in Pack '{settings.PackName}'.");
            return 0;
        }

        // Prüfen ob in anderem Pack
        var existingPack = library.Packs.Values
            .FirstOrDefault(p => p.Records.Contains(settings.RecordId));
        if (existingPack != null)
        {
            AnsiConsole.MarkupLine($"[yellow]Warnung:[/] Record '{settings.RecordId}' ist bereits in Pack '{existingPack.Name}'.");
            if (!AnsiConsole.Confirm("Trotzdem hinzufügen?", false))
            {
                return 1;
            }
        }

        pack.Records.Add(settings.RecordId);
        await libraryService.SaveAsync(library);

        AnsiConsole.MarkupLine($"[green]✓[/] Record '{settings.RecordId}' zu Pack '{settings.PackName}' hinzugefügt.");

        return 0;
    }
}

public class PackListCommand : AsyncCommand<PackListCommand.Settings>
{
    public class Settings : CommandSettings { }

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

        AnsiConsole.MarkupLine($"[blue]Bibliothek:[/] {appSettings.LibraryPath}");
        AnsiConsole.WriteLine();

        if (library.Packs.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]Keine Packs vorhanden.[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("Erstelle ein neues Pack mit: [yellow]fvb pack create <name>[/]");
            return 0;
        }

        var table = new Table();
        table.Border(TableBorder.Simple);
        table.AddColumn("Name");
        table.AddColumn("Resource");
        table.AddColumn("Records");
        table.AddColumn("Status");

        foreach (var pack in library.Packs.Values.OrderBy(p => p.Name))
        {
            var status = pack.Active ? "[green]aktiv[/]" : "[dim]inaktiv[/]";
            table.AddRow(
                pack.Name,
                pack.ResourceName,
                pack.Records.Count.ToString(),
                status);
        }

        AnsiConsole.Write(table);

        return 0;
    }
}

public class PackRemoveCommand : AsyncCommand<PackRemoveCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<pack>")]
        [Description("Pack-Name")]
        public required string PackName { get; set; }

        [CommandArgument(1, "<record>")]
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

        // Pack prüfen
        if (!library.Packs.TryGetValue(settings.PackName, out var pack))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Pack '{settings.PackName}' nicht gefunden.");
            return 1;
        }

        // Record prüfen
        if (!pack.Records.Contains(settings.RecordId))
        {
            AnsiConsole.MarkupLine($"[yellow]Hinweis:[/] Record '{settings.RecordId}' ist nicht in Pack '{settings.PackName}'.");
            return 0;
        }

        pack.Records.Remove(settings.RecordId);
        await libraryService.SaveAsync(library);

        AnsiConsole.MarkupLine($"[green]✓[/] Record '{settings.RecordId}' aus Pack '{settings.PackName}' entfernt.");

        return 0;
    }
}
