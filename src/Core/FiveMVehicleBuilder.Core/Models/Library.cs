namespace FiveMVehicleBuilder.Core.Models;

/// <summary>
/// Repräsentiert eine geöffnete Bibliothek mit allen Records, Packs und Einstellungen.
/// </summary>
public class Library
{
    /// <summary>
    /// Pfad zum Bibliotheks-Ordner
    /// </summary>
    public required string Path { get; set; }

    /// <summary>
    /// Bibliotheks-Einstellungen (library.json)
    /// </summary>
    public LibrarySettings Settings { get; set; } = new();

    /// <summary>
    /// Alle Records in der Bibliothek
    /// </summary>
    public Dictionary<string, Record> Records { get; set; } = new();

    /// <summary>
    /// Alle Packs in der Bibliothek
    /// </summary>
    public Dictionary<string, Pack> Packs { get; set; } = new();

    /// <summary>
    /// Pfade zu wichtigen Unterordnern
    /// </summary>
    public string RecordsPath => System.IO.Path.Combine(Path, "records");
    public string PacksPath => System.IO.Path.Combine(Path, "packs");
    public string PresetsPath => System.IO.Path.Combine(Path, "presets");
    public string CachePath => System.IO.Path.Combine(Path, ".cache");
    public string BuildPath => System.IO.Path.Combine(Path, "build");
    public string SettingsFilePath => System.IO.Path.Combine(Path, "library.json");
}
