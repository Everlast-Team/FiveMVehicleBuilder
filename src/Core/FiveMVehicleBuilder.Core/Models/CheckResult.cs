namespace FiveMVehicleBuilder.Core.Models;

/// <summary>
/// Ergebnis einer Prüfung
/// </summary>
public class CheckResult
{
    /// <summary>
    /// Alle gefundenen Befunde
    /// </summary>
    public List<CheckFinding> Findings { get; set; } = [];

    /// <summary>
    /// Zeitpunkt der Prüfung
    /// </summary>
    public DateTime CheckedAt { get; set; } = DateTime.Now;

    /// <summary>
    /// Anzahl der Fehler
    /// </summary>
    public int ErrorCount => Findings.Count(f => f.Severity == CheckSeverity.Error);

    /// <summary>
    /// Anzahl der Warnungen
    /// </summary>
    public int WarningCount => Findings.Count(f => f.Severity == CheckSeverity.Warning);

    /// <summary>
    /// Ob der Build blockiert ist (mindestens ein Fehler)
    /// </summary>
    public bool HasErrors => ErrorCount > 0;

    /// <summary>
    /// Ob die Prüfung erfolgreich war (keine Fehler)
    /// </summary>
    public bool IsSuccess => !HasErrors;
}

/// <summary>
/// Einzelner Befund einer Prüfung
/// </summary>
public class CheckFinding
{
    /// <summary>
    /// Schweregrad des Befunds
    /// </summary>
    public CheckSeverity Severity { get; set; }

    /// <summary>
    /// Prüfungs-Code (z.B. "DUPLICATE_SIREN_ID")
    /// </summary>
    public required string Code { get; set; }

    /// <summary>
    /// Menschenlesbare Beschreibung
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Betroffener Record (falls zutreffend)
    /// </summary>
    public string? RecordId { get; set; }

    /// <summary>
    /// Betroffene Variante (falls zutreffend)
    /// </summary>
    public string? VariantName { get; set; }

    /// <summary>
    /// Betroffene Datei (falls zutreffend)
    /// </summary>
    public string? FilePath { get; set; }
}

/// <summary>
/// Schweregrad eines Prüfbefunds
/// </summary>
public enum CheckSeverity
{
    /// <summary>
    /// Warnung - wird im Bericht ausgewiesen, blockiert Build nicht
    /// </summary>
    Warning,

    /// <summary>
    /// Fehler - blockiert den Build
    /// </summary>
    Error
}
