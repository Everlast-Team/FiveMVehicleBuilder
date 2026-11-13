namespace FiveMVehicleBuilder.Core.Services;

/// <summary>
/// Service für das Lesen und Extrahieren von GTA V RPF-Archiven
/// </summary>
public interface IRpfService
{
    /// <summary>
    /// Analysiert eine RPF-Datei und ermittelt die enthaltenen Fahrzeuge
    /// </summary>
    /// <param name="rpfPath">Pfad zur .rpf-Datei</param>
    /// <returns>Analyse-Ergebnis mit gefundenen Fahrzeugen</returns>
    Task<RpfAnalysis> AnalyzeAsync(string rpfPath);

    /// <summary>
    /// Extrahiert ein Fahrzeug aus der RPF in ein temporäres Verzeichnis
    /// </summary>
    /// <param name="vehicle">Zu extrahierendes Fahrzeug</param>
    /// <returns>Pfad zum temporären Verzeichnis</returns>
    Task<string> ExtractToTempAsync(RpfVehicleEntry vehicle);

    /// <summary>
    /// Räumt alle temporären Dateien auf
    /// </summary>
    void CleanupTemp();
}

/// <summary>
/// Ergebnis der RPF-Analyse
/// </summary>
public class RpfAnalysis
{
    /// <summary>
    /// Pfad zur analysierten RPF-Datei
    /// </summary>
    public required string RpfPath { get; set; }

    /// <summary>
    /// Gefundene Fahrzeuge in der RPF
    /// </summary>
    public List<RpfVehicleEntry> Vehicles { get; set; } = [];

    /// <summary>
    /// Warnungen während der Analyse
    /// </summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Ein in der RPF gefundenes Fahrzeug
/// </summary>
public class RpfVehicleEntry
{
    /// <summary>
    /// Modellname aus vehicles.meta
    /// </summary>
    public required string ModelName { get; set; }

    /// <summary>
    /// Handling-ID aus vehicles.meta
    /// </summary>
    public string? HandlingId { get; set; }

    /// <summary>
    /// GTA-Fahrzeugklassen-Name (z.B. "VC_EMERGENCY")
    /// </summary>
    public string? VehicleClassName { get; set; }

    /// <summary>
    /// Numerische GTA-Fahrzeugklasse (0-21)
    /// </summary>
    public int? VehicleClass { get; set; }

    /// <summary>
    /// Siren-Setting-ID (falls vorhanden)
    /// </summary>
    public int? SirenSettingId { get; set; }

    /// <summary>
    /// Dateien die zu diesem Fahrzeug gehören
    /// </summary>
    public List<RpfFileEntry> Files { get; set; } = [];

    /// <summary>
    /// Pfad zur Quell-RPF (für verschachtelte RPFs)
    /// </summary>
    public required string SourceRpfPath { get; set; }
}

/// <summary>
/// Eine Datei innerhalb der RPF
/// </summary>
public class RpfFileEntry
{
    /// <summary>
    /// Dateiname
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Relativer Pfad innerhalb der RPF (z.B. "vehicles.rpf/police.yft")
    /// </summary>
    public required string RelativePath { get; set; }

    /// <summary>
    /// Dateigröße in Bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Dateityp
    /// </summary>
    public RpfFileType Type { get; set; }
}

/// <summary>
/// Typ einer Datei in der RPF
/// </summary>
public enum RpfFileType
{
    /// <summary>
    /// Stream-Datei (.yft, .ytd)
    /// </summary>
    Stream,

    /// <summary>
    /// Meta-Datei (.meta)
    /// </summary>
    Meta,

    /// <summary>
    /// Audio-Datei (.rel, .awc)
    /// </summary>
    Audio,

    /// <summary>
    /// Sonstiger Typ
    /// </summary>
    Other
}
