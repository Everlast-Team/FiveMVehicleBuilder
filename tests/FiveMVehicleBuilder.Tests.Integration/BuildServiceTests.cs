using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Tests.Integration;

public class BuildServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _fixturesPath;
    private readonly BuildService _buildService;
    private readonly ImportService _importService;
    private readonly LibraryService _libraryService;

    public BuildServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"FiveMVehicleBuilder_Build_{Guid.NewGuid()}");
        _fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        var xmlValidationService = new XmlValidationService();
        _buildService = new BuildService(xmlValidationService);
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

        // Fahrzeug importieren
        var resourcePath = Path.Combine(_fixturesPath, "simple_vehicle");
        var analysis = await _importService.AnalyzeAsync(resourcePath);
        await _importService.ImportAsync(library, analysis.Vehicles[0], "einsatz");

        // Pack erstellen
        var pack = new Pack
        {
            Name = "einsatz",
            ResourceName = "vehicles_einsatz",
            Active = true,
            Records = [library.Records.Keys.First()]
        };
        library.Packs[pack.Name] = pack;

        return library;
    }

    [Fact]
    public async Task BuildPackAsync_CreatesOutputDirectory()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();

        // Act
        var result = await _buildService.BuildPackAsync(library, "einsatz");

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.OutputPath);
        Assert.True(Directory.Exists(result.OutputPath));
    }

    [Fact]
    public async Task BuildPackAsync_CreatesFxManifest()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();

        // Act
        var result = await _buildService.BuildPackAsync(library, "einsatz");

        // Assert
        var manifestPath = Path.Combine(result.OutputPath!, "fxmanifest.lua");
        Assert.True(File.Exists(manifestPath));

        var content = await File.ReadAllTextAsync(manifestPath);
        Assert.Contains("fx_version 'cerulean'", content);
        Assert.Contains("game 'gta5'", content);
        Assert.Contains("data/**/vehicles.meta", content);
    }

    [Fact]
    public async Task BuildPackAsync_CreatesStreamFiles()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();
        var record = library.Records.Values.First();
        var variant = record.Variants[0];

        // Act
        var result = await _buildService.BuildPackAsync(library, "einsatz");

        // Assert
        var variantStreamPath = Path.Combine(result.OutputPath!, "stream", variant.Name);
        Assert.True(Directory.Exists(variantStreamPath));
        Assert.True(File.Exists(Path.Combine(variantStreamPath, $"{variant.Name}.yft")));
    }

    [Fact]
    public async Task BuildPackAsync_CreatesMetaFiles()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();
        var record = library.Records.Values.First();
        var variant = record.Variants[0];

        // Act
        var result = await _buildService.BuildPackAsync(library, "einsatz");

        // Assert
        var variantDataPath = Path.Combine(result.OutputPath!, "data", variant.Name);
        Assert.True(Directory.Exists(variantDataPath));
        Assert.True(File.Exists(Path.Combine(variantDataPath, "vehicles.meta")));
        Assert.True(File.Exists(Path.Combine(variantDataPath, "handling.meta")));
    }

    [Fact]
    public async Task BuildPackAsync_TransformsVehiclesMeta()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();
        var record = library.Records.Values.First();
        var variant = record.Variants[0];

        // Act
        var result = await _buildService.BuildPackAsync(library, "einsatz");

        // Assert
        var vehiclesMetaPath = Path.Combine(result.OutputPath!, "data", variant.Name, "vehicles.meta");
        var content = await File.ReadAllTextAsync(vehiclesMetaPath);

        // Sollte den neuen Namen enthalten
        Assert.Contains($"<modelName>{variant.Name}</modelName>", content);
        Assert.Contains($"<txdName>{variant.Name}</txdName>", content);

        // Sollte den alten Namen NICHT enthalten
        Assert.DoesNotContain("<modelName>testcar</modelName>", content);
    }

    [Fact]
    public async Task BuildPackAsync_TransformsHandlingMeta()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();
        var record = library.Records.Values.First();
        var variant = record.Variants[0];

        // Act
        var result = await _buildService.BuildPackAsync(library, "einsatz");

        // Assert
        var handlingMetaPath = Path.Combine(result.OutputPath!, "data", variant.Name, "handling.meta");
        var content = await File.ReadAllTextAsync(handlingMetaPath);

        // Sollte die neue HandlingId enthalten
        Assert.Contains($"<handlingName>{record.HandlingId}</handlingName>", content);
    }

    [Fact]
    public async Task BuildPackAsync_ReturnsStatistics()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();

        // Act
        var result = await _buildService.BuildPackAsync(library, "einsatz");

        // Assert
        Assert.NotNull(result.Statistics);
        Assert.Equal(1, result.Statistics.RecordCount);
        Assert.Equal(1, result.Statistics.VariantCount);
        Assert.True(result.Statistics.TotalSizeBytes > 0);
        Assert.True(result.Statistics.Duration.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task BuildPackAsync_NonExistentPack_ReturnsError()
    {
        // Arrange
        var library = await _libraryService.CreateAsync(_testDirectory, initGit: false);

        // Act
        var result = await _buildService.BuildPackAsync(library, "nonexistent");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("nicht gefunden", result.ErrorMessage);
    }

    [Fact]
    public async Task BuildTestAsync_CreatesSingleRecordResource()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();
        var recordId = library.Records.Keys.First();

        // Act
        var result = await _buildService.BuildTestAsync(library, recordId);

        // Assert
        Assert.True(result.Success);
        Assert.Contains($"test_{recordId}", result.OutputPath);
        Assert.True(Directory.Exists(result.OutputPath));
    }

    [Fact]
    public async Task BuildTestAsync_DoesNotPersistTestPack()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();
        var recordId = library.Records.Keys.First();
        var packCountBefore = library.Packs.Count;

        // Act
        await _buildService.BuildTestAsync(library, recordId);

        // Assert
        Assert.Equal(packCountBefore, library.Packs.Count);
        Assert.False(library.Packs.ContainsKey($"test_{recordId}"));
    }

    [Fact]
    public async Task BuildPackAsync_OverwritesExistingBuild()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();

        // Ersten Build
        var result1 = await _buildService.BuildPackAsync(library, "einsatz");

        // Marker-Datei erstellen
        var markerPath = Path.Combine(result1.OutputPath!, "marker.txt");
        await File.WriteAllTextAsync(markerPath, "test");

        // Act - Zweiten Build
        var result2 = await _buildService.BuildPackAsync(library, "einsatz");

        // Assert
        Assert.True(result2.Success);
        Assert.False(File.Exists(markerPath)); // Marker sollte weg sein
    }

    [Fact]
    public async Task BuildPackAsync_ReportsProgress()
    {
        // Arrange
        var library = await SetupLibraryWithImportedVehicle();
        var progressReports = new List<BuildProgress>();
        var progress = new Progress<BuildProgress>(p => progressReports.Add(p));

        // Act
        await _buildService.BuildPackAsync(library, "einsatz", progress);

        // Kurz warten für async Progress
        await Task.Delay(100);

        // Assert
        Assert.NotEmpty(progressReports);
    }
}
