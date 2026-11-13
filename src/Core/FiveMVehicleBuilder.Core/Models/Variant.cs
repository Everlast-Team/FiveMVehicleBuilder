using System.Text.Json.Serialization;

namespace FiveMVehicleBuilder.Core.Models;

/// <summary>
/// Eine Variante ist ein baubares Fahrzeug, abgeleitet aus einem Record.
/// Jede Variante hat einen eigenen Spawnnamen, Siren-Setting und Livery-Zuordnungen.
/// </summary>
public class Variant
{
    /// <summary>
    /// Eindeutiger Spawnname der Variante (z.B. "ev_suv01a")
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Siren-Setting-ID für carcols.meta/carvariations.meta
    /// </summary>
    [JsonPropertyName("sirenSettingId")]
    public int? SirenSettingId { get; set; }

    /// <summary>
    /// Relativer Pfad zur Siren-Setting XML-Datei
    /// </summary>
    [JsonPropertyName("sirenSetting")]
    public string? SirenSetting { get; set; }

    /// <summary>
    /// Organisationstyp (police, medical, fire, etc.)
    /// </summary>
    [JsonPropertyName("orgType")]
    public string? OrgType { get; set; }

    /// <summary>
    /// Kategorie innerhalb der Organisation (Streifenwagen, RTW, Helikopter, etc.)
    /// </summary>
    [JsonPropertyName("orgCategory")]
    public string? OrgCategory { get; set; }

    /// <summary>
    /// Standard-Livery-Index (-1 = keine)
    /// </summary>
    [JsonPropertyName("defaultLivery")]
    public int? DefaultLivery { get; set; }

    /// <summary>
    /// Standard-Extras für diese Variante (Extra-Index -> aktiviert)
    /// </summary>
    [JsonPropertyName("extras")]
    public Dictionary<string, bool> Extras { get; set; } = new();

    /// <summary>
    /// Livery-Zuordnungen zu Departments
    /// </summary>
    [JsonPropertyName("liveries")]
    public List<LiveryAssignment> Liveries { get; set; } = [];
}

/// <summary>
/// Zuordnung einer Livery zu einem Department
/// </summary>
public class LiveryAssignment
{
    /// <summary>
    /// Livery-Index (1-basiert)
    /// </summary>
    [JsonPropertyName("index")]
    public int Index { get; set; }

    /// <summary>
    /// Department-Kürzel (z.B. "SAPD", "SAMS", "VFD")
    /// </summary>
    [JsonPropertyName("department")]
    public required string Department { get; set; }
}
