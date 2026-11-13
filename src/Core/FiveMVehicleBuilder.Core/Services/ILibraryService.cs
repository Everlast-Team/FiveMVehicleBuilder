using FiveMVehicleBuilder.Core.Models;

namespace FiveMVehicleBuilder.Core.Services;

/// <summary>
/// Service für Bibliotheks-Operationen: Öffnen, Erstellen, Speichern
/// </summary>
public interface ILibraryService
{
    /// <summary>
    /// Erstellt eine neue Bibliothek am angegebenen Pfad
    /// </summary>
    /// <param name="path">Pfad zum neuen Bibliotheks-Ordner</param>
    /// <param name="initGit">Git-Repository initialisieren</param>
    /// <returns>Die erstellte Bibliothek</returns>
    Task<Library> CreateAsync(string path, bool initGit = true);

    /// <summary>
    /// Öffnet eine bestehende Bibliothek
    /// </summary>
    /// <param name="path">Pfad zum Bibliotheks-Ordner</param>
    /// <returns>Die geöffnete Bibliothek</returns>
    Task<Library> OpenAsync(string path);

    /// <summary>
    /// Speichert alle Änderungen der Bibliothek
    /// </summary>
    /// <param name="library">Die zu speichernde Bibliothek</param>
    Task SaveAsync(Library library);

    /// <summary>
    /// Prüft ob am Pfad eine gültige Bibliothek existiert
    /// </summary>
    bool IsValidLibrary(string path);

    /// <summary>
    /// Prüft ob Git und Git LFS installiert sind
    /// </summary>
    Task<GitCheckResult> CheckGitInstallationAsync();
}

/// <summary>
/// Ergebnis der Git-Installations-Prüfung
/// </summary>
public class GitCheckResult
{
    public bool GitInstalled { get; set; }
    public bool GitLfsInstalled { get; set; }
    public string? GitVersion { get; set; }
    public string? GitLfsVersion { get; set; }

    public bool IsReady => GitInstalled && GitLfsInstalled;
}
