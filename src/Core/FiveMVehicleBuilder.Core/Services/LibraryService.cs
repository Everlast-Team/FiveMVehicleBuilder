using System.Diagnostics;
using System.Text.Json;
using FiveMVehicleBuilder.Core.Models;

namespace FiveMVehicleBuilder.Core.Services;

public class LibraryService : ILibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<Library> CreateAsync(string path, bool initGit = true)
    {
        if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
        {
            throw new InvalidOperationException($"Verzeichnis '{path}' existiert bereits und ist nicht leer.");
        }

        // Ordnerstruktur erstellen
        Directory.CreateDirectory(path);
        Directory.CreateDirectory(Path.Combine(path, "records"));
        Directory.CreateDirectory(Path.Combine(path, "packs"));
        Directory.CreateDirectory(Path.Combine(path, "presets", "sirens"));
        Directory.CreateDirectory(Path.Combine(path, "presets", "handling"));
        Directory.CreateDirectory(Path.Combine(path, "presets", "vehicles"));
        Directory.CreateDirectory(Path.Combine(path, ".cache"));
        Directory.CreateDirectory(Path.Combine(path, "build"));

        var library = new Library
        {
            Path = path,
            Settings = new LibrarySettings()
        };

        // library.json speichern
        await SaveSettingsAsync(library);

        if (initGit)
        {
            await InitGitRepositoryAsync(path);
        }

        return library;
    }

    public async Task<Library> OpenAsync(string path)
    {
        if (!IsValidLibrary(path))
        {
            throw new InvalidOperationException($"'{path}' ist keine gültige Bibliothek.");
        }

        var library = new Library { Path = path };

        // library.json laden
        var settingsPath = library.SettingsFilePath;
        if (File.Exists(settingsPath))
        {
            var json = await File.ReadAllTextAsync(settingsPath);
            library.Settings = JsonSerializer.Deserialize<LibrarySettings>(json, JsonOptions) ?? new LibrarySettings();
        }

        // Records laden
        await LoadRecordsAsync(library);

        // Packs laden
        await LoadPacksAsync(library);

        return library;
    }

    public async Task SaveAsync(Library library)
    {
        await SaveSettingsAsync(library);

        // Records speichern
        foreach (var record in library.Records.Values)
        {
            await SaveRecordAsync(library, record);
        }

        // Packs speichern
        foreach (var pack in library.Packs.Values)
        {
            await SavePackAsync(library, pack);
        }
    }

    public bool IsValidLibrary(string path)
    {
        if (!Directory.Exists(path))
            return false;

        // Mindestens records/ und library.json müssen existieren
        var recordsPath = Path.Combine(path, "records");
        var settingsPath = Path.Combine(path, "library.json");

        return Directory.Exists(recordsPath) && File.Exists(settingsPath);
    }

    public async Task<GitCheckResult> CheckGitInstallationAsync()
    {
        var result = new GitCheckResult();

        // Git prüfen
        try
        {
            var gitVersion = await RunCommandAsync("git", "--version");
            result.GitInstalled = true;
            result.GitVersion = gitVersion.Trim();
        }
        catch
        {
            result.GitInstalled = false;
        }

        // Git LFS prüfen
        try
        {
            var lfsVersion = await RunCommandAsync("git", "lfs version");
            result.GitLfsInstalled = true;
            result.GitLfsVersion = lfsVersion.Trim();
        }
        catch
        {
            result.GitLfsInstalled = false;
        }

        return result;
    }

    private async Task SaveSettingsAsync(Library library)
    {
        var json = JsonSerializer.Serialize(library.Settings, JsonOptions);
        await File.WriteAllTextAsync(library.SettingsFilePath, json);
    }

    private async Task LoadRecordsAsync(Library library)
    {
        var recordsPath = library.RecordsPath;
        if (!Directory.Exists(recordsPath))
            return;

        foreach (var recordDir in Directory.GetDirectories(recordsPath))
        {
            var recordJsonPath = Path.Combine(recordDir, "record.json");
            if (!File.Exists(recordJsonPath))
                continue;

            var json = await File.ReadAllTextAsync(recordJsonPath);
            var record = JsonSerializer.Deserialize<Record>(json, JsonOptions);
            if (record != null)
            {
                library.Records[record.Id] = record;
            }
        }
    }

    private async Task LoadPacksAsync(Library library)
    {
        var packsPath = library.PacksPath;
        if (!Directory.Exists(packsPath))
            return;

        foreach (var packFile in Directory.GetFiles(packsPath, "*.json"))
        {
            var json = await File.ReadAllTextAsync(packFile);
            var pack = JsonSerializer.Deserialize<Pack>(json, JsonOptions);
            if (pack != null)
            {
                library.Packs[pack.Name] = pack;
            }
        }
    }

    private async Task SaveRecordAsync(Library library, Record record)
    {
        var recordDir = Path.Combine(library.RecordsPath, record.Id);
        Directory.CreateDirectory(recordDir);

        var recordJsonPath = Path.Combine(recordDir, "record.json");
        var json = JsonSerializer.Serialize(record, JsonOptions);
        await File.WriteAllTextAsync(recordJsonPath, json);
    }

    private async Task SavePackAsync(Library library, Pack pack)
    {
        Directory.CreateDirectory(library.PacksPath);

        var packPath = Path.Combine(library.PacksPath, $"{pack.Name}.json");
        var json = JsonSerializer.Serialize(pack, JsonOptions);
        await File.WriteAllTextAsync(packPath, json);
    }

    private async Task InitGitRepositoryAsync(string path)
    {
        // Git init
        await RunCommandAsync("git", "init", path);

        // .gitignore erstellen
        var gitignoreContent = """
            # Cache und Build (werden regeneriert)
            .cache/
            build/
            """;
        await File.WriteAllTextAsync(Path.Combine(path, ".gitignore"), gitignoreContent);

        // Git LFS initialisieren
        await RunCommandAsync("git", "lfs install", path);

        // LFS für Binärdateien konfigurieren
        var gitattributesContent = """
            *.yft filter=lfs diff=lfs merge=lfs -text
            *.ytd filter=lfs diff=lfs merge=lfs -text
            """;
        await File.WriteAllTextAsync(Path.Combine(path, ".gitattributes"), gitattributesContent);

        // Initiale Dateien stagen
        await RunCommandAsync("git", "add .", path);
        await RunCommandAsync("git", "-c user.name=\"FiveMVehicleBuilder\" -c user.email=\"noreply@fivemvehiclebuilder.local\" commit -m \"Initial library setup\"", path);
    }

    private static async Task<string> RunCommandAsync(string command, string arguments, string? workingDirectory = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        if (workingDirectory != null)
        {
            psi.WorkingDirectory = workingDirectory;
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Konnte '{command}' nicht starten.");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Befehl '{command} {arguments}' fehlgeschlagen: {error}");
        }

        return output;
    }
}
