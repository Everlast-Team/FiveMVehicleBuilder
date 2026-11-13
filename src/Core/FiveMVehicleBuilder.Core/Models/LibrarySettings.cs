using System.Text.Json.Serialization;

namespace FiveMVehicleBuilder.Core.Models;

/// <summary>
/// Bibliotheks-Einstellungen (library.json)
/// </summary>
public class LibrarySettings
{
    /// <summary>
    /// Verfügbare Präfixe für Fahrzeug-IDs
    /// </summary>
    [JsonPropertyName("prefixes")]
    public Dictionary<string, PrefixSettings> Prefixes { get; set; } = new()
    {
        ["einsatz"] = new PrefixSettings { Prefix = "ev", NextNumber = 1 },
        ["zivil"] = new PrefixSettings { Prefix = "civ", NextNumber = 1 }
    };

    /// <summary>
    /// ID-Bereiche für verschiedene Ressourcen
    /// </summary>
    [JsonPropertyName("idRanges")]
    public IdRanges IdRanges { get; set; } = new();

    /// <summary>
    /// Schwellenwerte für Warnungen
    /// </summary>
    [JsonPropertyName("thresholds")]
    public Thresholds Thresholds { get; set; } = new();
}

/// <summary>
/// Einstellungen für ein Kategorie-Präfix
/// </summary>
public class PrefixSettings
{
    [JsonPropertyName("prefix")]
    public required string Prefix { get; set; }

    [JsonPropertyName("nextNumber")]
    public int NextNumber { get; set; } = 1;
}

/// <summary>
/// ID-Bereiche für Siren-Settings, Mod-Kits etc.
/// </summary>
public class IdRanges
{
    /// <summary>
    /// Start-ID für Siren-Settings
    /// </summary>
    [JsonPropertyName("sirenSettingIdStart")]
    public int SirenSettingIdStart { get; set; } = 1200;

    /// <summary>
    /// Nächste freie Siren-Setting-ID
    /// </summary>
    [JsonPropertyName("nextSirenSettingId")]
    public int NextSirenSettingId { get; set; } = 1200;

    /// <summary>
    /// Start-ID für Mod-Kits
    /// </summary>
    [JsonPropertyName("modKitIdStart")]
    public int ModKitIdStart { get; set; } = 50000;

    /// <summary>
    /// Nächste freie Mod-Kit-ID
    /// </summary>
    [JsonPropertyName("nextModKitId")]
    public int NextModKitId { get; set; } = 50000;
}

/// <summary>
/// Schwellenwerte für Prüfungen
/// </summary>
public class Thresholds
{
    /// <summary>
    /// Maximale Dateigröße in Bytes bevor eine Warnung ausgegeben wird
    /// Standard: 16 MB (FiveM Oversized-Asset-Warnung)
    /// </summary>
    [JsonPropertyName("maxFileSizeBytes")]
    public long MaxFileSizeBytes { get; set; } = 16 * 1024 * 1024;

    /// <summary>
    /// Maximale Länge für Spawnnamen
    /// </summary>
    [JsonPropertyName("maxSpawnNameLength")]
    public int MaxSpawnNameLength { get; set; } = 11;
}
