using System.Text.Json.Serialization;

namespace FiveMVehicleBuilder.Core.Models;

/// <summary>
/// Ein Record repräsentiert die Quelle eines Fahrzeugs mit allen Originaldateien
/// und abgeleiteten Varianten.
/// </summary>
public class Record
{
    /// <summary>
    /// Eindeutige ID des Records (z.B. "ev_suv01")
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Originalname aus der importierten Resource (z.B. "fpiu20")
    /// </summary>
    [JsonPropertyName("originalName")]
    public required string OriginalName { get; set; }

    /// <summary>
    /// Freitext: Woher das Fahrzeug stammt
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>
    /// Importdatum
    /// </summary>
    [JsonPropertyName("importedAt")]
    public DateOnly ImportedAt { get; set; }

    /// <summary>
    /// Kategorie des Fahrzeugs (z.B. "einsatz", "zivil")
    /// </summary>
    [JsonPropertyName("category")]
    public required string Category { get; set; }

    /// <summary>
    /// Handling-ID für vehicles.meta/handling.meta
    /// </summary>
    [JsonPropertyName("handlingId")]
    public required string HandlingId { get; set; }

    /// <summary>
    /// GTA-Fahrzeugklasse (0-21)
    /// </summary>
    [JsonPropertyName("vehicleClass")]
    public int? VehicleClass { get; set; }

    /// <summary>
    /// Benutzerdefinierter Preis für Autohäuser
    /// </summary>
    [JsonPropertyName("price")]
    public int? Price { get; set; }

    /// <summary>
    /// Liste der generierten Meta-Dateien (nicht aus Original)
    /// </summary>
    [JsonPropertyName("generatedFiles")]
    public List<string> GeneratedFiles { get; set; } = [];

    /// <summary>
    /// Letzter Prüfstatus
    /// </summary>
    [JsonPropertyName("checkStatus")]
    public CheckStatus? CheckStatus { get; set; }

    /// <summary>
    /// Alle Varianten dieses Records
    /// </summary>
    [JsonPropertyName("variants")]
    public List<Variant> Variants { get; set; } = [];
}

/// <summary>
/// Status der letzten Prüfung eines Records
/// </summary>
public class CheckStatus
{
    [JsonPropertyName("errors")]
    public int Errors { get; set; }

    [JsonPropertyName("warnings")]
    public int Warnings { get; set; }

    [JsonPropertyName("checkedAt")]
    public DateOnly CheckedAt { get; set; }
}
