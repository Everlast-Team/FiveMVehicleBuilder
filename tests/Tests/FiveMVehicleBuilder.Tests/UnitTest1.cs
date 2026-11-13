using System.Text.Json;
using FiveMVehicleBuilder.Core.Models;
using VehicleRecord = FiveMVehicleBuilder.Core.Models.Record;

namespace FiveMVehicleBuilder.Tests;

public class RecordTests
{
    [Fact]
    public void Record_CanBeCreated()
    {
        var record = new VehicleRecord
        {
            Id = "ev_suv01",
            OriginalName = "fpiu20",
            Category = "einsatz",
            HandlingId = "EV_SUV01",
            ImportedAt = DateOnly.FromDateTime(DateTime.Now)
        };

        Assert.Equal("ev_suv01", record.Id);
        Assert.Equal("fpiu20", record.OriginalName);
        Assert.Empty(record.Variants);
    }

    [Fact]
    public void Variant_CanBeAddedToRecord()
    {
        var record = new VehicleRecord
        {
            Id = "ev_suv01",
            OriginalName = "fpiu20",
            Category = "einsatz",
            HandlingId = "EV_SUV01"
        };

        var variant = new Variant
        {
            Name = "ev_suv01a",
            SirenSettingId = 1201
        };

        record.Variants.Add(variant);

        Assert.Single(record.Variants);
        Assert.Equal("ev_suv01a", record.Variants[0].Name);
    }

    [Fact]
    public void Record_SupportsVehicleClassAndPrice()
    {
        var record = new VehicleRecord
        {
            Id = "zv_sport01",
            OriginalName = "sports_car",
            Category = "zivil",
            HandlingId = "ZV_SPORT01",
            VehicleClass = 6, // Sport
            Price = 75000
        };

        Assert.Equal(6, record.VehicleClass);
        Assert.Equal(75000, record.Price);
    }

    [Fact]
    public void Record_VehicleClassAndPrice_CanBeNull()
    {
        var record = new VehicleRecord
        {
            Id = "ev_suv01",
            OriginalName = "fpiu20",
            Category = "einsatz",
            HandlingId = "EV_SUV01"
        };

        Assert.Null(record.VehicleClass);
        Assert.Null(record.Price);
    }

    [Fact]
    public void Record_SerializesWithNewProperties()
    {
        var record = new VehicleRecord
        {
            Id = "zv_sport01",
            OriginalName = "sports_car",
            Category = "zivil",
            HandlingId = "ZV_SPORT01",
            VehicleClass = 6,
            Price = 75000
        };

        var json = JsonSerializer.Serialize(record);

        Assert.Contains("\"vehicleClass\":6", json);
        Assert.Contains("\"price\":75000", json);
    }

    [Fact]
    public void Record_DeserializesWithNewProperties()
    {
        var json = """
        {
            "id": "zv_sport01",
            "originalName": "sports_car",
            "category": "zivil",
            "handlingId": "ZV_SPORT01",
            "vehicleClass": 6,
            "price": 75000,
            "variants": []
        }
        """;

        var record = JsonSerializer.Deserialize<VehicleRecord>(json);

        Assert.NotNull(record);
        Assert.Equal(6, record.VehicleClass);
        Assert.Equal(75000, record.Price);
    }

    [Fact]
    public void Record_BackwardsCompatible_DeserializesWithoutNewProperties()
    {
        var json = """
        {
            "id": "ev_suv01",
            "originalName": "fpiu20",
            "category": "einsatz",
            "handlingId": "EV_SUV01",
            "variants": []
        }
        """;

        var record = JsonSerializer.Deserialize<VehicleRecord>(json);

        Assert.NotNull(record);
        Assert.Null(record.VehicleClass);
        Assert.Null(record.Price);
    }
}

public class VariantTests
{
    [Fact]
    public void Variant_SupportsOrgProperties()
    {
        var variant = new Variant
        {
            Name = "ev_suv01a",
            SirenSettingId = 1201,
            OrgType = "police",
            OrgCategory = "Streifenwagen",
            DefaultLivery = 0
        };

        Assert.Equal("police", variant.OrgType);
        Assert.Equal("Streifenwagen", variant.OrgCategory);
        Assert.Equal(0, variant.DefaultLivery);
    }

    [Fact]
    public void Variant_OrgProperties_CanBeNull()
    {
        var variant = new Variant
        {
            Name = "ev_suv01a",
            SirenSettingId = 1201
        };

        Assert.Null(variant.OrgType);
        Assert.Null(variant.OrgCategory);
        Assert.Null(variant.DefaultLivery);
    }

    [Fact]
    public void Variant_SerializesWithOrgProperties()
    {
        var variant = new Variant
        {
            Name = "ev_suv01a",
            OrgType = "police",
            OrgCategory = "Streifenwagen",
            DefaultLivery = 0
        };

        var json = JsonSerializer.Serialize(variant);

        Assert.Contains("\"orgType\":\"police\"", json);
        Assert.Contains("\"orgCategory\":\"Streifenwagen\"", json);
        Assert.Contains("\"defaultLivery\":0", json);
    }

    [Fact]
    public void Variant_DeserializesWithOrgProperties()
    {
        var json = """
        {
            "name": "ev_suv01a",
            "sirenSettingId": 1201,
            "orgType": "medical",
            "orgCategory": "RTW",
            "defaultLivery": 1
        }
        """;

        var variant = JsonSerializer.Deserialize<Variant>(json);

        Assert.NotNull(variant);
        Assert.Equal("medical", variant.OrgType);
        Assert.Equal("RTW", variant.OrgCategory);
        Assert.Equal(1, variant.DefaultLivery);
    }

    [Fact]
    public void Variant_BackwardsCompatible_DeserializesWithoutOrgProperties()
    {
        var json = """
        {
            "name": "ev_suv01a",
            "sirenSettingId": 1201
        }
        """;

        var variant = JsonSerializer.Deserialize<Variant>(json);

        Assert.NotNull(variant);
        Assert.Null(variant.OrgType);
        Assert.Null(variant.OrgCategory);
        Assert.Null(variant.DefaultLivery);
    }
}

public class VehicleConstantsTests
{
    [Fact]
    public void VehicleClasses_Has22Entries()
    {
        Assert.Equal(22, VehicleConstants.VehicleClasses.Length);
    }

    [Fact]
    public void VehicleClassMapping_MapsCorrectly()
    {
        Assert.Equal(0, VehicleConstants.VehicleClassMapping["VC_COMPACT"]);
        Assert.Equal(18, VehicleConstants.VehicleClassMapping["VC_EMERGENCY"]);
        Assert.Equal(6, VehicleConstants.VehicleClassMapping["VC_SPORT"]);
        Assert.Equal(7, VehicleConstants.VehicleClassMapping["VC_SUPER"]);
    }

    [Fact]
    public void VehicleClassMapping_IsCaseInsensitive()
    {
        Assert.Equal(18, VehicleConstants.VehicleClassMapping["vc_emergency"]);
        Assert.Equal(18, VehicleConstants.VehicleClassMapping["VC_EMERGENCY"]);
        Assert.Equal(18, VehicleConstants.VehicleClassMapping["Vc_Emergency"]);
    }

    [Fact]
    public void OrgTypes_ReturnsReadOnlyList()
    {
        // Default provider returns empty list
        Assert.NotNull(VehicleConstants.OrgTypes);
    }

    [Fact]
    public void OrgCategories_ReturnsReadOnlyDictionary()
    {
        // Default provider returns empty dictionary
        Assert.NotNull(VehicleConstants.OrgCategories);
    }
}

public class PackTests
{
    [Fact]
    public void Pack_CanBeCreated()
    {
        var pack = new Pack
        {
            Name = "einsatz",
            ResourceName = "vehicles_einsatz"
        };

        Assert.Equal("einsatz", pack.Name);
        Assert.False(pack.Active);
        Assert.Empty(pack.Records);
    }
}

public class CheckResultTests
{
    [Fact]
    public void CheckResult_WithNoFindings_IsSuccess()
    {
        var result = new CheckResult();

        Assert.True(result.IsSuccess);
        Assert.False(result.HasErrors);
        Assert.Equal(0, result.ErrorCount);
        Assert.Equal(0, result.WarningCount);
    }

    [Fact]
    public void CheckResult_WithError_HasErrors()
    {
        var result = new CheckResult();
        result.Findings.Add(new CheckFinding
        {
            Severity = CheckSeverity.Error,
            Code = "DUPLICATE_ID",
            Message = "Doppelte ID gefunden"
        });

        Assert.False(result.IsSuccess);
        Assert.True(result.HasErrors);
        Assert.Equal(1, result.ErrorCount);
    }

    [Fact]
    public void CheckResult_WithWarning_IsStillSuccess()
    {
        var result = new CheckResult();
        result.Findings.Add(new CheckFinding
        {
            Severity = CheckSeverity.Warning,
            Code = "FILE_TOO_LARGE",
            Message = "Datei zu groß"
        });

        Assert.True(result.IsSuccess);
        Assert.False(result.HasErrors);
        Assert.Equal(1, result.WarningCount);
    }
}
