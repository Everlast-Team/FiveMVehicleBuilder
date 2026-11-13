using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;
using VehicleRecord = FiveMVehicleBuilder.Core.Models.Record;

namespace FiveMVehicleBuilder.Tests.Integration;

public class CheckServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _fixturesPath;
    private readonly CheckService _checkService;
    private readonly ImportService _importService;
    private readonly LibraryService _libraryService;

    public CheckServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"FiveMVehicleBuilder_Check_{Guid.NewGuid()}");
        _fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var xmlValidationService = new XmlValidationService();
        _checkService = new CheckService(xmlValidationService);
        _importService = new ImportService();
        _libraryService = new LibraryService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
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

    private async Task<Library> SetupLibraryWithImportedVehicle()
    {
        var library = await _libraryService.CreateAsync(_testDirectory, initGit: false);

        var resourcePath = Path.Combine(_fixturesPath, "simple_vehicle");
        var analysis = await _importService.AnalyzeAsync(resourcePath);
        await _importService.ImportAsync(library, analysis.Vehicles[0], "einsatz");

        return library;
    }

    [Fact]
    public async Task CheckRecordAsync_ValidRecord_NoErrors()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();
        var recordId = library.Records.Keys.First();

        // Act
        var result = await _checkService.CheckRecordAsync(library, recordId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ErrorCount);
    }

    [Fact]
    public async Task CheckRecordAsync_NonExistentRecord_ReturnsError()
    {
        // Arrange
        var library = await _libraryService.CreateAsync(_testDirectory, initGit: false);

        // Act
        var result = await _checkService.CheckRecordAsync(library, "nonexistent");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Findings, f => f.Code == "RECORD_NOT_FOUND");
    }

    [Fact]
    public async Task CheckAllAsync_ValidLibrary_NoErrors()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();

        // Act
        var result = await _checkService.CheckAllAsync(library);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckAllAsync_DuplicateSpawnNames_ReturnsError()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();
        var record = library.Records.Values.First();

        // Zweiten Record mit gleicher Variante hinzufügen
        var duplicateRecord = new VehicleRecord
        {
            Id = "duplicate_record",
            OriginalName = "duplicate",
            Category = "einsatz",
            HandlingId = "DUPLICATE"
        };
        duplicateRecord.Variants.Add(new Variant
        {
            Name = record.Variants[0].Name // Gleicher Name!
        });
        library.Records[duplicateRecord.Id] = duplicateRecord;

        // Act
        var result = await _checkService.CheckAllAsync(library);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Findings, f => f.Code == "DUPLICATE_SPAWN_NAME");
    }

    [Fact]
    public async Task CheckAllAsync_RecordInMultiplePacks_ReturnsError()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();
        var recordId = library.Records.Keys.First();

        // Record in zwei Packs
        library.Packs["pack1"] = new Pack
        {
            Name = "pack1",
            ResourceName = "pack1",
            Records = [recordId]
        };
        library.Packs["pack2"] = new Pack
        {
            Name = "pack2",
            ResourceName = "pack2",
            Records = [recordId]
        };

        // Act
        var result = await _checkService.CheckAllAsync(library);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Findings, f => f.Code == "RECORD_IN_MULTIPLE_PACKS");
    }

    [Fact]
    public async Task CheckActivePacksAsync_DuplicateSirenIds_ReturnsError()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();
        var record1 = library.Records.Values.First();

        // Zweiten Record mit gleicher Siren-ID hinzufügen
        var record2 = new VehicleRecord
        {
            Id = "second_record",
            OriginalName = "second",
            Category = "einsatz",
            HandlingId = "SECOND"
        };
        record2.Variants.Add(new Variant
        {
            Name = "second_a",
            SirenSettingId = record1.Variants[0].SirenSettingId // Gleiche ID!
        });
        library.Records[record2.Id] = record2;

        // Beide in aktivem Pack
        library.Packs["active"] = new Pack
        {
            Name = "active",
            ResourceName = "active",
            Active = true,
            Records = [record1.Id, record2.Id]
        };

        // Act
        var result = await _checkService.CheckActivePacksAsync(library);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Findings, f => f.Code == "DUPLICATE_SIREN_ID");
    }

    [Fact]
    public async Task CheckRecordAsync_SpawnNameTooLong_ReturnsWarning()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();
        var record = library.Records.Values.First();

        // Sehr langen Spawnnamen setzen
        record.Variants[0].Name = "very_long_spawn_name_that_exceeds_limit";

        // Act
        var result = await _checkService.CheckRecordAsync(library, record.Id);

        // Assert
        Assert.True(result.IsSuccess); // Warnung blockiert nicht
        Assert.Contains(result.Findings, f => f.Code == "SPAWN_NAME_TOO_LONG");
    }

    [Fact]
    public async Task CheckRecordAsync_MissingYft_ReturnsError()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();
        var record = library.Records.Values.First();

        // .yft löschen
        var yftPath = Path.Combine(library.RecordsPath, record.Id, "original", "stream", $"{record.OriginalName}.yft");
        File.Delete(yftPath);

        // Act
        var result = await _checkService.CheckRecordAsync(library, record.Id);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Findings, f => f.Code == "YFT_MISSING");
    }

    [Fact]
    public async Task CheckAllAsync_EmptyLibrary_NoErrors()
    {
        // Arrange
        var library = await _libraryService.CreateAsync(_testDirectory, initGit: false);

        // Act
        var result = await _checkService.CheckAllAsync(library);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
    }

    [Fact]
    public async Task CheckActivePacksAsync_NoActivePacks_NoErrors()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();

        // Inaktives Pack
        library.Packs["inactive"] = new Pack
        {
            Name = "inactive",
            ResourceName = "inactive",
            Active = false,
            Records = [library.Records.Keys.First()]
        };

        // Act
        var result = await _checkService.CheckActivePacksAsync(library);

        // Assert
        Assert.True(result.IsSuccess);
    }
}
