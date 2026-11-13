using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;
using VehicleRecord = FiveMVehicleBuilder.Core.Models.Record;

namespace FiveMVehicleBuilder.Tests.Integration;

public class LibraryServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly LibraryService _service;

    public LibraryServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"FiveMVehicleBuilder_Test_{Guid.NewGuid()}");
        _service = new LibraryService();
    }

    public void Dispose()
    {
        // Cleanup: Test-Verzeichnis löschen
        if (Directory.Exists(_testDirectory))
        {
            // Git-Dateien können schreibgeschützt sein
            SetAttributesNormal(new DirectoryInfo(_testDirectory));
            Directory.Delete(_testDirectory, recursive: true);
        }
    }

    private static void SetAttributesNormal(DirectoryInfo directory)
    {
        foreach (var subDir in directory.GetDirectories())
        {
            SetAttributesNormal(subDir);
        }
        foreach (var file in directory.GetFiles())
        {
            file.Attributes = FileAttributes.Normal;
        }
    }

    [Fact]
    public async Task CreateAsync_CreatesDirectoryStructure()
    {
        // Act
        var library = await _service.CreateAsync(_testDirectory, initGit: false);

        // Assert
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, "records")));
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, "packs")));
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, "presets", "sirens")));
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, "presets", "handling")));
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, "presets", "vehicles")));
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, ".cache")));
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, "build")));
        Assert.True(File.Exists(Path.Combine(_testDirectory, "library.json")));
    }

    [Fact]
    public async Task CreateAsync_CreatesLibraryJson()
    {
        // Act
        var library = await _service.CreateAsync(_testDirectory, initGit: false);

        // Assert
        var settingsPath = Path.Combine(_testDirectory, "library.json");
        Assert.True(File.Exists(settingsPath));

        var json = await File.ReadAllTextAsync(settingsPath);
        Assert.Contains("prefixes", json);
        Assert.Contains("idRanges", json);
        Assert.Contains("thresholds", json);
    }

    [Fact]
    public async Task CreateAsync_WithGit_InitializesRepository()
    {
        // Arrange - Check if git is available
        var gitCheck = await _service.CheckGitInstallationAsync();
        if (!gitCheck.IsReady)
        {
            // Skip test if git is not installed
            return;
        }

        // Act
        var library = await _service.CreateAsync(_testDirectory, initGit: true);

        // Assert
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, ".git")));
        Assert.True(File.Exists(Path.Combine(_testDirectory, ".gitignore")));
        Assert.True(File.Exists(Path.Combine(_testDirectory, ".gitattributes")));
    }

    [Fact]
    public async Task CreateAsync_ThrowsIfDirectoryNotEmpty()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        await File.WriteAllTextAsync(Path.Combine(_testDirectory, "existing.txt"), "test");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateAsync(_testDirectory, initGit: false));
    }

    [Fact]
    public async Task OpenAsync_LoadsLibrary()
    {
        // Arrange
        await _service.CreateAsync(_testDirectory, initGit: false);

        // Act
        var library = await _service.OpenAsync(_testDirectory);

        // Assert
        Assert.Equal(_testDirectory, library.Path);
        Assert.NotNull(library.Settings);
        Assert.Empty(library.Records);
        Assert.Empty(library.Packs);
    }

    [Fact]
    public async Task OpenAsync_ThrowsForInvalidLibrary()
    {
        // Arrange
        Directory.CreateDirectory(_testDirectory);
        // Keine library.json erstellt

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.OpenAsync(_testDirectory));
    }

    [Fact]
    public async Task SaveAsync_PersistsRecords()
    {
        // Arrange
        var library = await _service.CreateAsync(_testDirectory, initGit: false);
        var record = new VehicleRecord
        {
            Id = "ev_test01",
            OriginalName = "testcar",
            Category = "einsatz",
            HandlingId = "EV_TEST01",
            ImportedAt = DateOnly.FromDateTime(DateTime.Now)
        };
        record.Variants.Add(new Variant { Name = "ev_test01a" });
        library.Records[record.Id] = record;

        // Act
        await _service.SaveAsync(library);

        // Assert
        var recordPath = Path.Combine(_testDirectory, "records", "ev_test01", "record.json");
        Assert.True(File.Exists(recordPath));

        var json = await File.ReadAllTextAsync(recordPath);
        Assert.Contains("ev_test01", json);
        Assert.Contains("testcar", json);
        Assert.Contains("ev_test01a", json);
    }

    [Fact]
    public async Task SaveAsync_PersistsPacks()
    {
        // Arrange
        var library = await _service.CreateAsync(_testDirectory, initGit: false);
        var pack = new Pack
        {
            Name = "einsatz",
            ResourceName = "vehicles_einsatz",
            Active = true
        };
        pack.Records.Add("ev_test01");
        library.Packs[pack.Name] = pack;

        // Act
        await _service.SaveAsync(library);

        // Assert
        var packPath = Path.Combine(_testDirectory, "packs", "einsatz.json");
        Assert.True(File.Exists(packPath));

        var json = await File.ReadAllTextAsync(packPath);
        Assert.Contains("einsatz", json);
        Assert.Contains("vehicles_einsatz", json);
        Assert.Contains("ev_test01", json);
    }

    [Fact]
    public async Task OpenAsync_LoadsSavedRecords()
    {
        // Arrange
        var library = await _service.CreateAsync(_testDirectory, initGit: false);
        var record = new VehicleRecord
        {
            Id = "ev_test01",
            OriginalName = "testcar",
            Category = "einsatz",
            HandlingId = "EV_TEST01"
        };
        library.Records[record.Id] = record;
        await _service.SaveAsync(library);

        // Act
        var loadedLibrary = await _service.OpenAsync(_testDirectory);

        // Assert
        Assert.Single(loadedLibrary.Records);
        Assert.True(loadedLibrary.Records.ContainsKey("ev_test01"));
        Assert.Equal("testcar", loadedLibrary.Records["ev_test01"].OriginalName);
    }

    [Fact]
    public async Task IsValidLibrary_ReturnsTrueForValidLibrary()
    {
        // Arrange
        await _service.CreateAsync(_testDirectory, initGit: false);

        // Act
        var isValid = _service.IsValidLibrary(_testDirectory);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void IsValidLibrary_ReturnsFalseForNonExistentPath()
    {
        // Act
        var isValid = _service.IsValidLibrary(Path.Combine(_testDirectory, "nonexistent"));

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public async Task CheckGitInstallationAsync_ReturnsResult()
    {
        // Act
        var result = await _service.CheckGitInstallationAsync();

        // Assert - We just check that it doesn't throw and returns something
        Assert.NotNull(result);
        // GitInstalled depends on the environment, so we don't assert specific values
    }
}
