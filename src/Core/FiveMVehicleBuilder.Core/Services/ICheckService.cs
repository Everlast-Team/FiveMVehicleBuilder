using FiveMVehicleBuilder.Core.Models;

namespace FiveMVehicleBuilder.Core.Services;

/// <summary>
/// Service für Prüfungen von Records und Packs
/// </summary>
public interface ICheckService
{
    /// <summary>
    /// Prüft einen einzelnen Record
    /// </summary>
    /// <param name="library">Die Bibliothek</param>
    /// <param name="recordId">ID des zu prüfenden Records</param>
    /// <returns>Prüfergebnis</returns>
    Task<CheckResult> CheckRecordAsync(Library library, string recordId);

    /// <summary>
    /// Prüft alle Records in der Bibliothek
    /// </summary>
    /// <param name="library">Die Bibliothek</param>
    /// <returns>Prüfergebnis</returns>
    Task<CheckResult> CheckAllAsync(Library library);

    /// <summary>
    /// Prüft alle aktiven Packs auf Konflikte untereinander
    /// </summary>
    /// <param name="library">Die Bibliothek</param>
    /// <returns>Prüfergebnis</returns>
    Task<CheckResult> CheckActivePacksAsync(Library library);

    /// <summary>
    /// Prüft alle Varianten in der Bibliothek auf doppelte SirenIDs (global, nicht nur aktive Packs)
    /// </summary>
    /// <param name="library">Die Bibliothek</param>
    /// <returns>Prüfergebnis mit DUPLICATE_SIREN_ID_GLOBAL Findings</returns>
    Task<CheckResult> CheckAllSirenIdsAsync(Library library);

    /// <summary>
    /// Prüft die XML-Validität aller Meta-Dateien eines Records
    /// </summary>
    /// <param name="library">Die Bibliothek</param>
    /// <param name="recordId">ID des zu prüfenden Records</param>
    /// <returns>Prüfergebnis mit INVALID_XML Findings</returns>
    Task<CheckResult> CheckMetaFileValidityAsync(Library library, string recordId);

    /// <summary>
    /// Behebt alle doppelten SirenIDs durch Vergabe neuer eindeutiger IDs
    /// </summary>
    /// <param name="library">Die Bibliothek</param>
    /// <returns>Anzahl der gefixten Varianten</returns>
    Task<int> FixDuplicateSirenIdsAsync(Library library);
}
