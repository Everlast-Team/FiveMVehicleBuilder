using FiveMVehicleBuilder.Core.Models;

namespace FiveMVehicleBuilder.Core.Services;

/// <summary>
/// Service für den Import von FiveM-Resources in die Bibliothek
/// </summary>
public interface IImportService
{
    /// <summary>
    /// Analysiert eine Resource und ermittelt die enthaltenen Fahrzeuge
    /// </summary>
    /// <param name="resourcePath">Pfad zur FiveM-Resource</param>
    /// <returns>Analyse-Ergebnis mit gefundenen Fahrzeugen und Problemen</returns>
    Task<ImportAnalysis> AnalyzeAsync(string resourcePath);

    /// <summary>
    /// Importiert ein analysiertes Fahrzeug in die Bibliothek
    /// </summary>
    /// <param name="library">Ziel-Bibliothek</param>
    /// <param name="vehicle">Zu importierendes Fahrzeug aus der Analyse</param>
    /// <param name="category">Kategorie (z.B. "einsatz", "zivil")</param>
    /// <param name="source">Quellen-Beschreibung</param>
    /// <param name="customName">Benutzerdefinierter Name (optional, sonst automatisch generiert)</param>
    /// <returns>Der erstellte Record</returns>
    Task<Record> ImportAsync(Library library, AnalyzedVehicle vehicle, string category, string? source = null, string? customName = null);
}

/// <summary>
/// Ergebnis der Resource-Analyse
/// </summary>
public class ImportAnalysis
{
    /// <summary>
    /// Pfad zur analysierten Resource
    /// </summary>
    public required string ResourcePath { get; set; }

    /// <summary>
    /// Gefundene, importierbare Fahrzeuge
    /// </summary>
    public List<AnalyzedVehicle> Vehicles { get; set; } = [];

    /// <summary>
    /// Fahrzeuge die nicht importiert werden können (mit Grund)
    /// </summary>
    public List<SkippedVehicle> SkippedVehicles { get; set; } = [];

    /// <summary>
    /// Stream-Dateien die keinem Fahrzeug zugeordnet werden konnten
    /// </summary>
    public List<string> OrphanedFiles { get; set; } = [];

    /// <summary>
    /// Allgemeine Probleme bei der Analyse
    /// </summary>
    public List<string> Warnings { get; set; } = [];
}

/// <summary>
/// Ein in der Resource gefundenes, importierbares Fahrzeug
/// </summary>
public class AnalyzedVehicle
{
    /// <summary>
    /// Modellname (aus vehicles.meta oder Dateiname)
    /// </summary>
    public required string ModelName { get; set; }

    /// <summary>
    /// Gefundene Stream-Dateien
    /// </summary>
    public required VehicleStreamFiles StreamFiles { get; set; }

    /// <summary>
    /// Pfad zur vehicles.meta (null wenn nicht vorhanden, wird dann generiert)
    /// </summary>
    public string? VehiclesMetaPath { get; set; }

    /// <summary>
    /// Handling-ID aus vehicles.meta
    /// </summary>
    public string? HandlingId { get; set; }

    /// <summary>
    /// Pfad zur handling.meta (falls vorhanden)
    /// </summary>
    public string? HandlingMetaPath { get; set; }

    /// <summary>
    /// Pfad zur carvariations.meta (falls vorhanden)
    /// </summary>
    public string? CarVariationsMetaPath { get; set; }

    /// <summary>
    /// Pfad zur carcols.meta (falls vorhanden)
    /// </summary>
    public string? CarColsMetaPath { get; set; }

    /// <summary>
    /// Gefundene Siren-Setting-ID (falls vorhanden)
    /// </summary>
    public int? SirenSettingId { get; set; }

    /// <summary>
    /// GTA-Fahrzeugklassen-Name aus vehicles.meta (z.B. "VC_EMERGENCY")
    /// </summary>
    public string? VehicleClassName { get; set; }

    /// <summary>
    /// Numerische GTA-Fahrzeugklasse (0-21)
    /// </summary>
    public int? VehicleClass { get; set; }

    /// <summary>
    /// True wenn Meta-Dateien beim Import generiert werden müssen
    /// </summary>
    public bool NeedsMetaGeneration => VehiclesMetaPath == null;
}

/// <summary>
/// Stream-Dateien eines Fahrzeugs
/// </summary>
public class VehicleStreamFiles
{
    public required string YftPath { get; set; }
    public string? HiYftPath { get; set; }
    public string? YtdPath { get; set; }
    public string? HiYtdPath { get; set; }
    /// <summary>
    /// Zusätzliche Dateien (Anbau-Elemente wie modelname+door.yft)
    /// </summary>
    public List<string> AdditionalFiles { get; set; } = [];
}

/// <summary>
/// Ein Fahrzeug das nicht importiert werden kann
/// </summary>
public class SkippedVehicle
{
    public required string ModelName { get; set; }
    public required string Reason { get; set; }
}
