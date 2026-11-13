using System.Text.Json.Serialization;

namespace FiveMVehicleBuilder.Core.Models;

/// <summary>
/// Ein Pack ist eine benannte Zusammenstellung von Records.
/// Ein Record liegt mit allen seinen Varianten in genau einem Pack.
/// </summary>
public class Pack
{
    /// <summary>
    /// Name des Packs (z.B. "einsatz", "zivil")
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    /// <summary>
    /// Name der zu generierenden FiveM-Resource (z.B. "vehicles_einsatz")
    /// </summary>
    [JsonPropertyName("resourceName")]
    public required string ResourceName { get; set; }

    /// <summary>
    /// Ob das Pack aktiv ist (läuft auf dem Server)
    /// </summary>
    [JsonPropertyName("active")]
    public bool Active { get; set; }

    /// <summary>
    /// Liste der Record-IDs in diesem Pack
    /// </summary>
    [JsonPropertyName("records")]
    public List<string> Records { get; set; } = [];
}
