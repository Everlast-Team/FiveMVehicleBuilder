using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;

using VehicleRecord = FiveMVehicleBuilder.Core.Models.Record;

namespace FiveMVehicleBuilder.Tests.Integration;

public class ExportServiceTests : IDisposable
{
    private readonly string _testPath;
    private readonly LibraryService _libraryService;
    private readonly ExportService _exportService;

    public ExportServiceTests()
    {
        _testPath = Path.Combine(Path.GetTempPath(), $"fvb_export_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testPath);
        _libraryService = new LibraryService();
        _exportService = new ExportService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testPath))
        {
            SetAttributesNormal(new DirectoryInfo(_testPath));
            Directory.Delete(_testPath, true);
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

    private async Task<Library> CreateTestLibraryWithPack()
    {
        var library = await _libraryService.CreateAsync(_testPath, initGit: false);

        // Records mit Varianten erstellen
        var record1 = new VehicleRecord
        {
            Id = "ev_suv01",
            OriginalName = "fpiu20",
            Category = "einsatz",
            HandlingId = "EV_SUV01",
            ImportedAt = DateOnly.FromDateTime(DateTime.Now),
            Variants =
            [
                new Variant
                {
                    Name = "ev_suv01a",
                    SirenSettingId = 1201,
                    Liveries =
                    [
                        new LiveryAssignment { Index = 1, Department = "SAPD" },
                        new LiveryAssignment { Index = 2, Department = "SAMS" }
                    ]
                },
                new Variant
                {
                    Name = "ev_suv01b",
                    SirenSettingId = 1202
                }
            ]
        };

        var record2 = new VehicleRecord
        {
            Id = "ev_rtw01",
            OriginalName = "ambulance",
            Category = "einsatz",
            HandlingId = "EV_RTW01",
            ImportedAt = DateOnly.FromDateTime(DateTime.Now),
            Variants =
            [
                new Variant
                {
                    Name = "ev_rtw01a",
                    SirenSettingId = 1301
                }
            ]
        };

        library.Records["ev_suv01"] = record1;
        library.Records["ev_rtw01"] = record2;

        // Pack erstellen
        var pack = new Pack
        {
            Name = "einsatz",
            ResourceName = "vehicles_einsatz",
            Active = true,
            Records = ["ev_suv01", "ev_rtw01"]
        };

        library.Packs["einsatz"] = pack;

        await _libraryService.SaveAsync(library);
        return library;
    }

    [Fact]
    public async Task ExportAsync_PackNotFound_ReturnsError()
    {
        var library = await _libraryService.CreateAsync(_testPath, initGit: false);

        var result = await _exportService.ExportAsync(library, "nonexistent", ExportFormat.Json);

        Assert.False(result.Success);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Fact]
    public async Task ExportAsync_JsonFormat_CreatesValidFile()
    {
        var library = await CreateTestLibraryWithPack();

        var result = await _exportService.ExportAsync(library, "einsatz", ExportFormat.Json);

        Assert.True(result.Success);
        Assert.NotNull(result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));
        Assert.Equal(3, result.VehicleCount); // 2 variants from ev_suv01 + 1 from ev_rtw01

        var content = await File.ReadAllTextAsync(result.OutputPath);
        Assert.Contains("ev_suv01a", content);
        Assert.Contains("ev_suv01b", content);
        Assert.Contains("ev_rtw01a", content);
    }

    [Fact]
    public async Task ExportAsync_LuaFormat_CreatesValidFile()
    {
        var library = await CreateTestLibraryWithPack();

        var result = await _exportService.ExportAsync(library, "einsatz", ExportFormat.Lua);

        Assert.True(result.Success);
        Assert.NotNull(result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));

        var content = await File.ReadAllTextAsync(result.OutputPath);
        Assert.Contains("local Vehicles = {", content);
        Assert.Contains("model = \"ev_suv01a\"", content);
        Assert.Contains("name = \"FPIU20\"", content);
        Assert.Contains("return Vehicles", content);
    }

    [Fact]
    public async Task ExportAsync_SqlFormat_CreatesValidFile()
    {
        var library = await CreateTestLibraryWithPack();

        var result = await _exportService.ExportAsync(library, "einsatz", ExportFormat.Sql);

        Assert.True(result.Success);
        Assert.NotNull(result.OutputPath);
        Assert.True(File.Exists(result.OutputPath));

        var content = await File.ReadAllTextAsync(result.OutputPath);
        Assert.Contains("INSERT INTO vehicles", content);
        Assert.Contains("ev_suv01a", content);
    }

    [Fact]
    public async Task ExportAsync_CustomOutputPath_UsesSpecifiedPath()
    {
        var library = await CreateTestLibraryWithPack();
        var customPath = Path.Combine(_testPath, "custom", "export.json");

        var result = await _exportService.ExportAsync(library, "einsatz", ExportFormat.Json, customPath);

        Assert.True(result.Success);
        Assert.Equal(customPath, result.OutputPath);
        Assert.True(File.Exists(customPath));
    }

    [Fact]
    public async Task ExportAsync_JsonFormat_IncludesLiveries()
    {
        var library = await CreateTestLibraryWithPack();

        var result = await _exportService.ExportAsync(library, "einsatz", ExportFormat.Json);

        Assert.True(result.Success);
        var content = await File.ReadAllTextAsync(result.OutputPath!);
        Assert.Contains("SAPD", content);
        Assert.Contains("SAMS", content);
    }

    [Fact]
    public async Task ExportAsync_LuaFormat_IncludesLiveries()
    {
        var library = await CreateTestLibraryWithPack();

        var result = await _exportService.ExportAsync(library, "einsatz", ExportFormat.Lua);

        Assert.True(result.Success);
        var content = await File.ReadAllTextAsync(result.OutputPath!);
        Assert.Contains("liveries = {", content);
        Assert.Contains("[1] = \"SAPD\"", content);
        Assert.Contains("[2] = \"SAMS\"", content);
    }

    [Fact]
    public async Task ExportAsync_UsesRecordPriceIfSet()
    {
        var library = await CreateTestLibraryWithPackAndPrices();

        var result = await _exportService.ExportAsync(library, "zivil", ExportFormat.Json);

        Assert.True(result.Success);
        var content = await File.ReadAllTextAsync(result.OutputPath!);
        // Should use the custom price of 75000, not the estimated price
        Assert.Contains("75000", content);
    }

    private async Task<Library> CreateTestLibraryWithPackAndPrices()
    {
        var library = await _libraryService.CreateAsync(_testPath, initGit: false);

        var record = new VehicleRecord
        {
            Id = "zv_sport01",
            OriginalName = "sports_car",
            Category = "zivil",
            HandlingId = "ZV_SPORT01",
            ImportedAt = DateOnly.FromDateTime(DateTime.Now),
            VehicleClass = 6, // Sport
            Price = 75000,
            Variants =
            [
                new Variant
                {
                    Name = "zv_sport01a"
                }
            ]
        };

        library.Records["zv_sport01"] = record;

        var pack = new Pack
        {
            Name = "zivil",
            ResourceName = "vehicles_zivil",
            Active = true,
            Records = ["zv_sport01"]
        };

        library.Packs["zivil"] = pack;

        await _libraryService.SaveAsync(library);
        return library;
    }
}
