using System.Text.Json;

namespace FiveMVehicleBuilder.Core;

/// <summary>
/// Anwendungseinstellungen (gespeichert in %APPDATA%)
/// </summary>
public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "FiveMVehicleBuilder",
        "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// Pfad zur aktuellen Bibliothek
    /// </summary>
    public string? LibraryPath { get; set; }

    /// <summary>
    /// Lädt die Einstellungen
    /// </summary>
    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    /// <summary>
    /// Speichert die Einstellungen
    /// </summary>
    public void Save()
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }
}
