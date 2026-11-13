using Spectre.Console;
using Spectre.Console.Cli;
using System.ComponentModel;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Cli.Commands;

public class RecordInfoCommand : AsyncCommand<RecordInfoCommand.Settings>
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

        if (!library.Records.TryGetValue(settings.RecordId, out var record))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Record '{settings.RecordId}' nicht gefunden.");
            return 1;
        }

        // Header
        AnsiConsole.Write(new Rule($"[blue]{record.Id}[/]").LeftJustified());
        AnsiConsole.WriteLine();

        // Basis-Infos
        var grid = new Grid();
        grid.AddColumn();
        grid.AddColumn();

        grid.AddRow("[dim]Original:[/]", record.OriginalName);
        grid.AddRow("[dim]Kategorie:[/]", record.Category);
        grid.AddRow("[dim]Handling-ID:[/]", record.HandlingId);
        grid.AddRow("[dim]Importiert:[/]", record.ImportedAt.ToString("yyyy-MM-dd"));
        if (!string.IsNullOrEmpty(record.Source))
        {
            grid.AddRow("[dim]Quelle:[/]", record.Source);
        }

        AnsiConsole.Write(grid);
        AnsiConsole.WriteLine();

        // Pack-Zugehörigkeit
        var pack = library.Packs.Values
            .FirstOrDefault(p => p.Records.Contains(record.Id));
        if (pack != null)
        {
            AnsiConsole.MarkupLine($"[dim]Pack:[/] {pack.Name} ({pack.ResourceName})");
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]Pack:[/] [yellow]keinem Pack zugeordnet[/]");
        }
        AnsiConsole.WriteLine();

        // Varianten
        AnsiConsole.MarkupLine($"[blue]Varianten ({record.Variants.Count}):[/]");
        var table = new Table();
        table.Border(TableBorder.Simple);
        table.AddColumn("Spawn-Name");
        table.AddColumn("Siren-ID");
        table.AddColumn("Liveries");

        foreach (var variant in record.Variants.OrderBy(v => v.Name))
        {
            var liveries = variant.Liveries.Count > 0
                ? string.Join(", ", variant.Liveries.Select(l => $"{l.Index}:{l.Department}"))
                : "-";

            table.AddRow(
                variant.Name,
                variant.SirenSettingId?.ToString() ?? "-",
                liveries);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        // Dateien
        var recordPath = Path.Combine(appSettings.LibraryPath, "records", record.Id, "original");
        if (Directory.Exists(recordPath))
        {
            var streamPath = Path.Combine(recordPath, "stream");
            var dataPath = Path.Combine(recordPath, "data");

            AnsiConsole.MarkupLine("[blue]Dateien:[/]");

            if (Directory.Exists(streamPath))
            {
                var streamFiles = Directory.GetFiles(streamPath);
                foreach (var file in streamFiles)
                {
                    var size = new FileInfo(file).Length;
                    AnsiConsole.MarkupLine($"  [dim]stream/[/]{Path.GetFileName(file)} [dim]({size / 1024.0:F1} KB)[/]");
                }
            }

            if (Directory.Exists(dataPath))
            {
                var dataFiles = Directory.GetFiles(dataPath);
                foreach (var file in dataFiles)
                {
                    AnsiConsole.MarkupLine($"  [dim]data/[/]{Path.GetFileName(file)}");
                }
            }
        }

        return 0;
    }
}

public class RecordRemoveCommand : AsyncCommand<RecordRemoveCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<record>")]
        [Description("Record-ID")]
        public required string RecordId { get; set; }

        [CommandOption("--force")]
        [Description("Ohne Bestätigung löschen")]
        public bool Force { get; set; }
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

        if (!library.Records.TryGetValue(settings.RecordId, out var record))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Record '{settings.RecordId}' nicht gefunden.");
            return 1;
        }

        // Prüfen ob in einem Pack
        var pack = library.Packs.Values
            .FirstOrDefault(p => p.Records.Contains(record.Id));

        if (pack != null)
        {
            AnsiConsole.MarkupLine($"[yellow]Warnung:[/] Record ist in Pack '{pack.Name}'.");
        }

        // Bestätigung
        if (!settings.Force)
        {
            AnsiConsole.MarkupLine($"[yellow]Record '{record.Id}' ({record.OriginalName}) wird gelöscht.[/]");
            AnsiConsole.MarkupLine($"  Varianten: {record.Variants.Count}");

            if (!AnsiConsole.Confirm("Fortfahren?", false))
            {
                AnsiConsole.MarkupLine("[dim]Abgebrochen.[/]");
                return 0;
            }
        }

        // Aus Pack entfernen falls vorhanden
        if (pack != null)
        {
            pack.Records.Remove(record.Id);
        }

        // Record-Verzeichnis löschen
        var recordPath = Path.Combine(appSettings.LibraryPath, "records", record.Id);
        if (Directory.Exists(recordPath))
        {
            Directory.Delete(recordPath, true);
        }

        // Aus Library entfernen
        library.Records.Remove(record.Id);
        await libraryService.SaveAsync(library);

        AnsiConsole.MarkupLine($"[green]✓[/] Record '{record.Id}' gelöscht.");

        return 0;
    }
}

public class RecordMoveCommand : AsyncCommand<RecordMoveCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandArgument(0, "<record>")]
        [Description("Record-ID")]
        public required string RecordId { get; set; }

        [CommandArgument(1, "<pack>")]
        [Description("Ziel-Pack")]
        public required string PackName { get; set; }
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
        if (!library.Records.ContainsKey(settings.RecordId))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Record '{settings.RecordId}' nicht gefunden.");
            return 1;
        }

        // Ziel-Pack prüfen
        if (!library.Packs.TryGetValue(settings.PackName, out var targetPack))
        {
            AnsiConsole.MarkupLine($"[red]Fehler:[/] Pack '{settings.PackName}' nicht gefunden.");
            return 1;
        }

        // Aus aktuellem Pack entfernen
        var currentPack = library.Packs.Values
            .FirstOrDefault(p => p.Records.Contains(settings.RecordId));

        if (currentPack != null)
        {
            if (currentPack.Name == settings.PackName)
            {
                AnsiConsole.MarkupLine($"[yellow]Hinweis:[/] Record ist bereits in Pack '{settings.PackName}'.");
                return 0;
            }
            currentPack.Records.Remove(settings.RecordId);
        }

        // Zu Ziel-Pack hinzufügen
        if (!targetPack.Records.Contains(settings.RecordId))
        {
            targetPack.Records.Add(settings.RecordId);
        }

        await libraryService.SaveAsync(library);

        if (currentPack != null)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Record '{settings.RecordId}' von '{currentPack.Name}' nach '{settings.PackName}' verschoben.");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]✓[/] Record '{settings.RecordId}' zu Pack '{settings.PackName}' hinzugefügt.");
        }

        return 0;
    }
}
