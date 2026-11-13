using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Tests.Integration;

public class ImportServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _fixturesPath;
    private readonly ImportService _importService;
    private readonly LibraryService _libraryService;

    public ImportServiceTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"FiveMVehicleBuilder_Import_{Guid.NewGuid()}");
        _fixturesPath = Path.Combine(AppContext.BaseDirectory, "Fixtures");
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

    [Fact]
    public async Task AnalyzeAsync_SimpleVehicle_FindsVehicle()
    {
        // Arrange
        var resourcePath = Path.Combine(_fixturesPath, "simple_vehicle");

        // Act
        var analysis = await _importService.AnalyzeAsync(resourcePath);

        // Assert
        Assert.Single(analysis.Vehicles);
        Assert.Empty(analysis.SkippedVehicles);
        Assert.Empty(analysis.OrphanedFiles);

        var vehicle = analysis.Vehicles[0];
        Assert.Equal("testcar", vehicle.ModelName);
        Assert.Equal("TESTCAR", vehicle.HandlingId);
        Assert.Equal(1200, vehicle.SirenSettingId);
        Assert.NotNull(vehicle.StreamFiles.YftPath);
        Assert.NotNull(vehicle.StreamFiles.HiYftPath);
        Assert.NotNull(vehicle.StreamFiles.YtdPath);
    }

    [Fact]
    public async Task AnalyzeAsync_MultiVehicle_FindsAllVehicles()
    {
        // Arrange
        var resourcePath = Path.Combine(_fixturesPath, "multi_vehicle");

        // Act
        var analysis = await _importService.AnalyzeAsync(resourcePath);

        // Assert
        Assert.Equal(2, analysis.Vehicles.Count);
        Assert.Empty(analysis.SkippedVehicles);

        var police1 = analysis.Vehicles.FirstOrDefault(v => v.ModelName == "police1");
        var police2 = analysis.Vehicles.FirstOrDefault(v => v.ModelName == "police2");

        Assert.NotNull(police1);
        Assert.NotNull(police2);
        Assert.Equal("POLICE1", police1.HandlingId);
        Assert.Equal("POLICE2", police2.HandlingId);
        Assert.Equal(1201, police1.SirenSettingId);
        Assert.Equal(1202, police2.SirenSettingId);
    }

    [Fact]
    public async Task AnalyzeAsync_NonExistentPath_ReturnsWarning()
    {
        // Arrange
        var resourcePath = Path.Combine(_testDirectory, "nonexistent");

        // Act
        var analysis = await _importService.AnalyzeAsync(resourcePath);

        // Assert
        Assert.Empty(analysis.Vehicles);
        Assert.NotEmpty(analysis.Warnings);
    }

    [Fact]
    public async Task AnalyzeAsync_MissingYft_SkipsVehicle()
    {
        // Arrange - Create a resource without .yft files
        var resourcePath = Path.Combine(_testDirectory, "missing_yft");
        var dataPath = Path.Combine(resourcePath, "data");
        Directory.CreateDirectory(dataPath);

        var vehiclesMeta = """
            <?xml version="1.0" encoding="UTF-8"?>
            <CVehicleModelInfo__InitDataList>
              <InitDatas>
                <Item>
                  <modelName>missingcar</modelName>
                  <txdName>missingcar</txdName>
                  <handlingId>MISSINGCAR</handlingId>
                </Item>
              </InitDatas>
            </CVehicleModelInfo__InitDataList>
            """;
        await File.WriteAllTextAsync(Path.Combine(dataPath, "vehicles.meta"), vehiclesMeta);

        // Act
        var analysis = await _importService.AnalyzeAsync(resourcePath);

        // Assert
        Assert.Empty(analysis.Vehicles);
        Assert.Single(analysis.SkippedVehicles);
        Assert.Contains("Keine .yft-Datei", analysis.SkippedVehicles[0].Reason);
    }

    [Fact]
    public async Task ImportAsync_CreatesRecord()
    {
        // Arrange
        var library = await _libraryService.CreateAsync(_testDirectory, initGit: false);
        var resourcePath = Path.Combine(_fixturesPath, "simple_vehicle");
        var analysis = await _importService.AnalyzeAsync(resourcePath);
        var vehicle = analysis.Vehicles[0];

        // Act
        var record = await _importService.ImportAsync(library, vehicle, "einsatz", "Test Import");

        // Assert
        Assert.NotNull(record);
        Assert.Equal("testcar", record.OriginalName);
        Assert.Equal("einsatz", record.Category);
        Assert.Equal("Test Import", record.Source);
        Assert.Single(record.Variants);
        Assert.EndsWith("a", record.Variants[0].Name);
    }

    [Fact]
    public async Task ImportAsync_CopiesFiles()
    {
        // Arrange
        var library = await _libraryService.CreateAsync(_testDirectory, initGit: false);
        var resourcePath = Path.Combine(_fixturesPath, "simple_vehicle");
        var analysis = await _importService.AnalyzeAsync(resourcePath);
        var vehicle = analysis.Vehicles[0];

        // Act
        var record = await _importService.ImportAsync(library, vehicle, "einsatz");

        // Assert
        var recordPath = Path.Combine(library.RecordsPath, record.Id, "original");
        Assert.True(File.Exists(Path.Combine(recordPath, "stream", "testcar.yft")));
        Assert.True(File.Exists(Path.Combine(recordPath, "stream", "testcar_hi.yft")));
        Assert.True(File.Exists(Path.Combine(recordPath, "stream", "testcar.ytd")));
        Assert.True(File.Exists(Path.Combine(recordPath, "data", "vehicles.meta")));
        Assert.True(File.Exists(Path.Combine(recordPath, "data", "handling.meta")));
    }

    [Fact]
    public async Task ImportAsync_IncrementsNextNumber()
    {
        // Arrange
        var library = await _libraryService.CreateAsync(_testDirectory, initGit: false);
        var resourcePath = Path.Combine(_fixturesPath, "simple_vehicle");
        var analysis = await _importService.AnalyzeAsync(resourcePath);
        var vehicle = analysis.Vehicles[0];

        var initialNumber = library.Settings.Prefixes["einsatz"].NextNumber;

        // Act
        await _importService.ImportAsync(library, vehicle, "einsatz");

        // Assert
        Assert.Equal(initialNumber + 1, library.Settings.Prefixes["einsatz"].NextNumber);
    }

    [Fact]
    public async Task ImportAsync_AddsRecordToLibrary()
    {
        // Arrange
        var library = await _libraryService.CreateAsync(_testDirectory, initGit: false);
        var resourcePath = Path.Combine(_fixturesPath, "simple_vehicle");
        var analysis = await _importService.AnalyzeAsync(resourcePath);
        var vehicle = analysis.Vehicles[0];

        // Act
        var record = await _importService.ImportAsync(library, vehicle, "einsatz");

        // Assert
        Assert.True(library.Records.ContainsKey(record.Id));
        Assert.Same(record, library.Records[record.Id]);
    }

    [Fact]
    public async Task ImportAsync_SetsVariantSirenSettingId()
    {
        // Arrange
        var library = await _libraryService.CreateAsync(_testDirectory, initGit: false);
        var resourcePath = Path.Combine(_fixturesPath, "simple_vehicle");
        var analysis = await _importService.AnalyzeAsync(resourcePath);
        var vehicle = analysis.Vehicles[0];

        // Act
        var record = await _importService.ImportAsync(library, vehicle, "einsatz");

        // Assert
        Assert.Equal(1200, record.Variants[0].SirenSettingId);
    }

    [Fact]
    public async Task AnalyzeAsync_ParsesVehicleClass()
    {
        // Arrange
        var resourcePath = Path.Combine(_fixturesPath, "simple_vehicle");

        // Act
        var analysis = await _importService.AnalyzeAsync(resourcePath);

        // Assert
        Assert.Single(analysis.Vehicles);
        var vehicle = analysis.Vehicles[0];
        Assert.Equal("VC_EMERGENCY", vehicle.VehicleClassName);
        Assert.Equal(18, vehicle.VehicleClass); // VC_EMERGENCY = 18
    }

    [Fact]
    public async Task ImportAsync_SetsRecordVehicleClass()
    {
        // Arrange
        var library = await _libraryService.CreateAsync(_testDirectory, initGit: false);
        var resourcePath = Path.Combine(_fixturesPath, "simple_vehicle");
        var analysis = await _importService.AnalyzeAsync(resourcePath);
        var vehicle = analysis.Vehicles[0];

        // Act
        var record = await _importService.ImportAsync(library, vehicle, "einsatz");

        // Assert
        Assert.Equal(18, record.VehicleClass); // VC_EMERGENCY = 18
    }

    [Fact]
    public async Task AnalyzeAsync_VehicleWithoutClass_HasNullVehicleClass()
    {
        // Arrange - Create a resource without vehicleClass element
        var resourcePath = Path.Combine(_testDirectory, "no_vehicleclass");
        var dataPath = Path.Combine(resourcePath, "data");
        var streamPath = Path.Combine(resourcePath, "stream");
        Directory.CreateDirectory(dataPath);
        Directory.CreateDirectory(streamPath);

        var vehiclesMeta = """
            <?xml version="1.0" encoding="UTF-8"?>
            <CVehicleModelInfo__InitDataList>
              <InitDatas>
                <Item>
                  <modelName>noclass</modelName>
                  <txdName>noclass</txdName>
                  <handlingId>NOCLASS</handlingId>
                </Item>
              </InitDatas>
            </CVehicleModelInfo__InitDataList>
            """;
        await File.WriteAllTextAsync(Path.Combine(dataPath, "vehicles.meta"), vehiclesMeta);

        // Create dummy stream files
        await File.WriteAllTextAsync(Path.Combine(streamPath, "noclass.yft"), "dummy");
        await File.WriteAllTextAsync(Path.Combine(streamPath, "noclass.ytd"), "dummy");

        // Act
        var analysis = await _importService.AnalyzeAsync(resourcePath);

        // Assert
        Assert.Single(analysis.Vehicles);
        var vehicle = analysis.Vehicles[0];
        Assert.Null(vehicle.VehicleClassName);
        Assert.Null(vehicle.VehicleClass);
    }

    [Fact]
    public async Task AnalyzeAsync_RecursiveSearch_FindsFilesInNestedDirectories()
    {
        // Arrange - Create nested directory structure
        var resourcePath = Path.Combine(_testDirectory, "nested_structure");
        var nestedDataPath = Path.Combine(resourcePath, "subfolder", "another", "data");
        var nestedStreamPath = Path.Combine(resourcePath, "different", "path", "stream");
        Directory.CreateDirectory(nestedDataPath);
        Directory.CreateDirectory(nestedStreamPath);

        var vehiclesMeta = """
            <?xml version="1.0" encoding="UTF-8"?>
            <CVehicleModelInfo__InitDataList>
              <InitDatas>
                <Item>
                  <modelName>nestedcar</modelName>
                  <txdName>nestedcar</txdName>
                  <handlingId>NESTEDCAR</handlingId>
                </Item>
              </InitDatas>
            </CVehicleModelInfo__InitDataList>
            """;
        await File.WriteAllTextAsync(Path.Combine(nestedDataPath, "vehicles.meta"), vehiclesMeta);
        await File.WriteAllTextAsync(Path.Combine(nestedStreamPath, "nestedcar.yft"), "dummy");
        await File.WriteAllTextAsync(Path.Combine(nestedStreamPath, "nestedcar.ytd"), "dummy");

        // Act
        var analysis = await _importService.AnalyzeAsync(resourcePath);

        // Assert
        Assert.Single(analysis.Vehicles);
        Assert.Equal("nestedcar", analysis.Vehicles[0].ModelName);
    }

    [Fact]
    public async Task AnalyzeAsync_NoVehiclesMeta_DetectsVehicleFromYftFilename()
    {
        // Arrange - Create resource with only stream files, no meta
        var resourcePath = Path.Combine(_testDirectory, "no_meta");
        var streamPath = Path.Combine(resourcePath, "stream");
        Directory.CreateDirectory(streamPath);

        await File.WriteAllTextAsync(Path.Combine(streamPath, "nometa_car.yft"), "dummy");
        await File.WriteAllTextAsync(Path.Combine(streamPath, "nometa_car.ytd"), "dummy");

        // Act
        var analysis = await _importService.AnalyzeAsync(resourcePath);

        // Assert
        Assert.Single(analysis.Vehicles);
        Assert.Equal("nometa_car", analysis.Vehicles[0].ModelName);
        Assert.True(analysis.Vehicles[0].NeedsMetaGeneration);
        Assert.Null(analysis.Vehicles[0].VehiclesMetaPath);
        Assert.Contains(analysis.Warnings, w => w.Contains("Keine vehicles.meta gefunden"));
    }

    [Fact]
    public async Task AnalyzeAsync_WithVehiclesMeta_NeedsMetaGenerationIsFalse()
    {
        // Arrange
        var resourcePath = Path.Combine(_fixturesPath, "simple_vehicle");

        // Act
        var analysis = await _importService.AnalyzeAsync(resourcePath);

        // Assert
        Assert.Single(analysis.Vehicles);
        Assert.False(analysis.Vehicles[0].NeedsMetaGeneration);
        Assert.NotNull(analysis.Vehicles[0].VehiclesMetaPath);
    }

    [Fact]
    public async Task ImportAsync_NoVehiclesMeta_GeneratesTemplateMetaFiles()
    {
        // Arrange - Create resource with only stream files
        var resourcePath = Path.Combine(_testDirectory, "generate_meta_resource");
        var streamPath = Path.Combine(resourcePath, "stream");
        Directory.CreateDirectory(streamPath);

        await File.WriteAllTextAsync(Path.Combine(streamPath, "gencar.yft"), "dummy");
        await File.WriteAllTextAsync(Path.Combine(streamPath, "gencar.ytd"), "dummy");

        var libraryPath = Path.Combine(_testDirectory, "generate_meta_library");
        var library = await _libraryService.CreateAsync(libraryPath, initGit: false);
        var analysis = await _importService.AnalyzeAsync(resourcePath);
        var vehicle = analysis.Vehicles[0];

        // Act
        var record = await _importService.ImportAsync(library, vehicle, "einsatz", customName: "ev_gencar");

        // Assert - Meta files should be generated
        var recordPath = Path.Combine(library.RecordsPath, record.Id, "original");
        var vehiclesMetaPath = Path.Combine(recordPath, "data", "vehicles.meta");
        var handlingMetaPath = Path.Combine(recordPath, "data", "handling.meta");

        Assert.True(File.Exists(vehiclesMetaPath));
        Assert.True(File.Exists(handlingMetaPath));

        // Verify content contains model name
        var vehiclesMetaContent = await File.ReadAllTextAsync(vehiclesMetaPath);
        Assert.Contains("gencar", vehiclesMetaContent);
        Assert.Contains("EV_GENCAR", vehiclesMetaContent); // handlingId should be uppercase record name

        var handlingMetaContent = await File.ReadAllTextAsync(handlingMetaPath);
        Assert.Contains("EV_GENCAR", handlingMetaContent);
    }

    [Fact]
    public async Task AnalyzeAsync_NonStandardMetaFileNames_FindsVehicles()
    {
        // Arrange - Create resource with non-standard meta file names (like pbikeveh.meta instead of vehicles.meta)
        var resourcePath = Path.Combine(_testDirectory, "nonstandard_meta");
        Directory.CreateDirectory(resourcePath);

        // Meta file with non-standard name containing two vehicles
        var vehiclesMeta = """
            <?xml version="1.0" encoding="UTF-8"?>
            <CVehicleModelInfo__InitDataList>
              <residentTxd>vehshare</residentTxd>
              <InitDatas>
                <Item>
                  <modelName>policeb1</modelName>
                  <txdName>policeb1</txdName>
                  <handlingId>BATI</handlingId>
                  <vehicleClass>VC_EMERGENCY</vehicleClass>
                </Item>
                <Item>
                  <modelName>policeb2</modelName>
                  <txdName>policeb2</txdName>
                  <handlingId>BF400</handlingId>
                  <vehicleClass>VC_EMERGENCY</vehicleClass>
                </Item>
              </InitDatas>
            </CVehicleModelInfo__InitDataList>
            """;
        await File.WriteAllTextAsync(Path.Combine(resourcePath, "pbikeveh.meta"), vehiclesMeta);

        // Car variations meta with non-standard name
        var carVarMeta = """
            <?xml version="1.0" encoding="UTF-8"?>
            <CVehicleModelInfoVariation>
              <variationData>
                <Item>
                  <modelName>policeb1</modelName>
                  <sirenSettings value="33"/>
                </Item>
                <Item>
                  <modelName>policeb2</modelName>
                  <sirenSettings value="33"/>
                </Item>
              </variationData>
            </CVehicleModelInfoVariation>
            """;
        await File.WriteAllTextAsync(Path.Combine(resourcePath, "pbikecarvar.meta"), carVarMeta);

        // Stream files - only for policeb2 (policeb1 should be skipped)
        await File.WriteAllTextAsync(Path.Combine(resourcePath, "policeb2.yft"), "dummy");
        await File.WriteAllTextAsync(Path.Combine(resourcePath, "policeb2.ytd"), "dummy");
        await File.WriteAllTextAsync(Path.Combine(resourcePath, "policeb2_hi.yft"), "dummy");

        // Act
        var analysis = await _importService.AnalyzeAsync(resourcePath);

        // Assert
        Assert.Single(analysis.Vehicles); // Only policeb2 (has .yft)
        Assert.Single(analysis.SkippedVehicles); // policeb1 (no .yft)

        var vehicle = analysis.Vehicles[0];
        Assert.Equal("policeb2", vehicle.ModelName);
        Assert.Equal("BF400", vehicle.HandlingId);
        Assert.Equal("VC_EMERGENCY", vehicle.VehicleClassName);
        Assert.Equal(18, vehicle.VehicleClass); // VC_EMERGENCY = 18
        Assert.Equal(33, vehicle.SirenSettingId);
        Assert.NotNull(vehicle.VehiclesMetaPath);
        Assert.Contains("pbikeveh.meta", vehicle.VehiclesMetaPath);
        Assert.False(vehicle.NeedsMetaGeneration);

        // Check skipped vehicle
        Assert.Equal("policeb1", analysis.SkippedVehicles[0].ModelName);
        Assert.Contains("Keine .yft-Datei", analysis.SkippedVehicles[0].Reason);
    }

    [Fact]
    public async Task AnalyzeAsync_FilesInRootDirectory_FindsVehicles()
    {
        // Arrange - Create resource with all files in root directory (no stream/ or data/ subdirs)
        var resourcePath = Path.Combine(_testDirectory, "root_files");
        Directory.CreateDirectory(resourcePath);

        var vehiclesMeta = """
            <?xml version="1.0" encoding="UTF-8"?>
            <CVehicleModelInfo__InitDataList>
              <InitDatas>
                <Item>
                  <modelName>rootcar</modelName>
                  <txdName>rootcar</txdName>
                  <handlingId>ROOTCAR</handlingId>
                </Item>
              </InitDatas>
            </CVehicleModelInfo__InitDataList>
            """;
        await File.WriteAllTextAsync(Path.Combine(resourcePath, "somename.meta"), vehiclesMeta);
        await File.WriteAllTextAsync(Path.Combine(resourcePath, "rootcar.yft"), "dummy");
        await File.WriteAllTextAsync(Path.Combine(resourcePath, "rootcar.ytd"), "dummy");

        // Act
        var analysis = await _importService.AnalyzeAsync(resourcePath);

        // Assert
        Assert.Single(analysis.Vehicles);
        Assert.Equal("rootcar", analysis.Vehicles[0].ModelName);
        Assert.Equal("ROOTCAR", analysis.Vehicles[0].HandlingId);
    }

}
