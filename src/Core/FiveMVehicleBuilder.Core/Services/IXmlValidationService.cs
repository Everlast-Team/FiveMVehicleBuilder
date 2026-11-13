namespace FiveMVehicleBuilder.Core.Services;

/// <summary>
/// Service zur Validierung und Reparatur von XML-Dateien
/// </summary>
public interface IXmlValidationService
{
    /// <summary>
    /// Validiert XML-Content
    /// </summary>
    /// <param name="content">XML-Inhalt</param>
    /// <param name="filePath">Dateipfad (für Fehlermeldungen)</param>
    /// <returns>Validierungsergebnis</returns>
    XmlValidationResult ValidateXml(string content, string? filePath = null);

    /// <summary>
    /// Validiert eine XML-Datei
    /// </summary>
    /// <param name="filePath">Pfad zur Datei</param>
    /// <returns>Validierungsergebnis</returns>
    Task<XmlValidationResult> ValidateXmlFileAsync(string filePath);

    /// <summary>
    /// Versucht häufige XML-Fehler zu reparieren
    /// </summary>
    /// <param name="content">Fehlerhafter XML-Inhalt</param>
    /// <returns>Reparierter Inhalt oder null wenn nicht reparierbar</returns>
    string? TryRepairXml(string content);
}

/// <summary>
/// Ergebnis einer XML-Validierung
/// </summary>
public class XmlValidationResult
{
    /// <summary>
    /// Ob das XML valide ist
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Fehlermeldung (falls nicht valide)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Zeilennummer des Fehlers (1-basiert)
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Position in der Zeile (1-basiert)
    /// </summary>
    public int? LinePosition { get; set; }

    /// <summary>
    /// Dateipfad (falls angegeben)
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Formatierte Fehlermeldung mit Zeilen-/Positionsangabe
    /// </summary>
    public string FormattedError
    {
        get
        {
            if (IsValid)
                return string.Empty;

            var location = LineNumber.HasValue
                ? $" (Zeile {LineNumber}, Position {LinePosition})"
                : string.Empty;

            var file = !string.IsNullOrEmpty(FilePath)
                ? $"[{Path.GetFileName(FilePath)}] "
                : string.Empty;

            return $"{file}{ErrorMessage}{location}";
        }
    }

    /// <summary>
    /// Erstellt ein erfolgreiches Ergebnis
    /// </summary>
    public static XmlValidationResult Success(string? filePath = null) => new()
    {
        IsValid = true,
        FilePath = filePath
    };

    /// <summary>
    /// Erstellt ein Fehlerergebnis
    /// </summary>
    public static XmlValidationResult Failure(string errorMessage, int? lineNumber = null, int? linePosition = null, string? filePath = null) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage,
        LineNumber = lineNumber,
        LinePosition = linePosition,
        FilePath = filePath
    };
}
