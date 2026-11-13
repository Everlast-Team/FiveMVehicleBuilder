using FiveMVehicleBuilder.Core.Models;

namespace FiveMVehicleBuilder.Core.Services;

/// <summary>
/// Service für das Bauen von FiveM-Resources aus Packs
/// </summary>
public interface IBuildService
{
    /// <summary>
    /// Baut eine FiveM-Resource aus einem Pack
    /// </summary>
    /// <param name="library">Die Bibliothek</param>
    /// <param name="packName">Name des zu bauenden Packs</param>
    /// <param name="progress">Optional: Fortschritts-Callback</param>
    /// <returns>Build-Ergebnis</returns>
    Task<BuildResult> BuildPackAsync(Library library, string packName, IProgress<BuildProgress>? progress = null);

    /// <summary>
    /// Baut eine Test-Resource für einen einzelnen Record
    /// </summary>
    /// <param name="library">Die Bibliothek</param>
    /// <param name="recordId">ID des zu testenden Records</param>
    /// <param name="progress">Optional: Fortschritts-Callback</param>
    /// <returns>Build-Ergebnis</returns>
    Task<BuildResult> BuildTestAsync(Library library, string recordId, IProgress<BuildProgress>? progress = null);
}

/// <summary>
/// Ergebnis eines Builds
/// </summary>
public class BuildResult
{
    /// <summary>
    /// Ob der Build erfolgreich war
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Pfad zur gebauten Resource
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Prüfergebnis (falls Prüfung fehlgeschlagen)
    /// </summary>
    public CheckResult? CheckResult { get; set; }

    /// <summary>
    /// Fehlermeldung (falls Build fehlgeschlagen)
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Statistiken zum Build
    /// </summary>
    public BuildStatistics? Statistics { get; set; }
}

/// <summary>
/// Statistiken eines Builds
/// </summary>
public class BuildStatistics
{
    public int RecordCount { get; set; }
    public int VariantCount { get; set; }
    public long TotalSizeBytes { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Fortschritt eines Builds
/// </summary>
public class BuildProgress
{
    public required string Stage { get; set; }
    public required string CurrentItem { get; set; }
    public int Current { get; set; }
    public int Total { get; set; }
    public double Percentage => Total > 0 ? (double)Current / Total * 100 : 0;
}
