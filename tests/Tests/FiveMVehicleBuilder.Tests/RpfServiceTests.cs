using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.Tests;

public class RpfServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_WithNonExistentFile_ReturnsWarning()
    {
        var service = new RpfService();
        var result = await service.AnalyzeAsync(@"C:\NonExistent\test.rpf");

        Assert.Empty(result.Vehicles);
        Assert.Single(result.Warnings);
        Assert.Contains("existiert nicht", result.Warnings[0]);
    }

    [Fact]
    public void CleanupTemp_DoesNotThrow_WhenNoTempDirectories()
    {
        var service = new RpfService();

        // Should not throw
        service.CleanupTemp();
    }

    [Fact]
    public void RpfAnalysis_CanBeCreated()
    {
        var analysis = new RpfAnalysis
        {
            RpfPath = @"C:\test\dlc.rpf"
        };

        Assert.Equal(@"C:\test\dlc.rpf", analysis.RpfPath);
        Assert.Empty(analysis.Vehicles);
        Assert.Empty(analysis.Warnings);
    }

    [Fact]
    public void RpfVehicleEntry_CanBeCreated()
    {
        var entry = new RpfVehicleEntry
        {
            ModelName = "police",
            HandlingId = "POLICE",
            VehicleClassName = "VC_EMERGENCY",
            VehicleClass = 18,
            SirenSettingId = 1201,
            SourceRpfPath = @"C:\test\dlc.rpf"
        };

        Assert.Equal("police", entry.ModelName);
        Assert.Equal("POLICE", entry.HandlingId);
        Assert.Equal("VC_EMERGENCY", entry.VehicleClassName);
        Assert.Equal(18, entry.VehicleClass);
        Assert.Equal(1201, entry.SirenSettingId);
        Assert.Empty(entry.Files);
    }

    [Fact]
    public void RpfFileEntry_CanBeCreated()
    {
        var entry = new RpfFileEntry
        {
            Name = "police.yft",
            RelativePath = "x64/levels/gta5/vehicles.rpf/police.yft",
            Size = 1024000,
            Type = RpfFileType.Stream
        };

        Assert.Equal("police.yft", entry.Name);
        Assert.Equal("x64/levels/gta5/vehicles.rpf/police.yft", entry.RelativePath);
        Assert.Equal(1024000, entry.Size);
        Assert.Equal(RpfFileType.Stream, entry.Type);
    }

    [Fact]
    public void RpfFileType_HasExpectedValues()
    {
        Assert.Equal(RpfFileType.Stream, (RpfFileType)0);
        Assert.Equal(RpfFileType.Meta, (RpfFileType)1);
        Assert.Equal(RpfFileType.Audio, (RpfFileType)2);
        Assert.Equal(RpfFileType.Other, (RpfFileType)3);
    }

    [Fact]
    public void RpfVehicleEntry_Files_CanBeAdded()
    {
        var entry = new RpfVehicleEntry
        {
            ModelName = "police",
            SourceRpfPath = @"C:\test\dlc.rpf"
        };

        entry.Files.Add(new RpfFileEntry
        {
            Name = "police.yft",
            RelativePath = "vehicles.rpf/police.yft",
            Size = 500000,
            Type = RpfFileType.Stream
        });

        entry.Files.Add(new RpfFileEntry
        {
            Name = "police.ytd",
            RelativePath = "vehicles.rpf/police.ytd",
            Size = 200000,
            Type = RpfFileType.Stream
        });

        entry.Files.Add(new RpfFileEntry
        {
            Name = "vehicles.meta",
            RelativePath = "common/data/vehicles.meta",
            Size = 50000,
            Type = RpfFileType.Meta
        });

        Assert.Equal(3, entry.Files.Count);
        Assert.Equal(2, entry.Files.Count(f => f.Type == RpfFileType.Stream));
        Assert.Single(entry.Files, f => f.Type == RpfFileType.Meta);
    }

    [Fact]
    public void RpfAnalysis_CanHoldMultipleVehicles()
    {
        var analysis = new RpfAnalysis
        {
            RpfPath = @"C:\test\dlc.rpf"
        };

        analysis.Vehicles.Add(new RpfVehicleEntry
        {
            ModelName = "police",
            SourceRpfPath = analysis.RpfPath
        });

        analysis.Vehicles.Add(new RpfVehicleEntry
        {
            ModelName = "police2",
            SourceRpfPath = analysis.RpfPath
        });

        analysis.Vehicles.Add(new RpfVehicleEntry
        {
            ModelName = "ambulance",
            SourceRpfPath = analysis.RpfPath
        });

        Assert.Equal(3, analysis.Vehicles.Count);
    }

    [Fact]
    public void RpfAnalysis_CanHoldWarnings()
    {
        var analysis = new RpfAnalysis
        {
            RpfPath = @"C:\test\dlc.rpf"
        };

        analysis.Warnings.Add("Missing .yft for vehicle1");
        analysis.Warnings.Add("Corrupted meta file");

        Assert.Equal(2, analysis.Warnings.Count);
    }

    [Fact]
    public void RpfVehicleEntry_VehicleClass_CanBeNull()
    {
        var entry = new RpfVehicleEntry
        {
            ModelName = "custom_car",
            SourceRpfPath = @"C:\test\dlc.rpf"
        };

        Assert.Null(entry.VehicleClass);
        Assert.Null(entry.VehicleClassName);
        Assert.Null(entry.SirenSettingId);
        Assert.Null(entry.HandlingId);
    }

    [Fact]
    public void RpfService_ImplementsInterface()
    {
        var service = new RpfService();
        Assert.IsAssignableFrom<IRpfService>(service);
    }
}
