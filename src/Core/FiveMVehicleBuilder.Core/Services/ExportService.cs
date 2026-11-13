using System.Text;
using System.Text.Json;
using FiveMVehicleBuilder.Core.Models;

namespace FiveMVehicleBuilder.Core.Services;

public interface IExportService
{
    /// <summary>
    /// Gets available export format IDs.
    /// </summary>
    IReadOnlyList<string> GetAvailableFormats();

    /// <summary>
    /// Gets display name for a format ID.
    /// </summary>
    string GetFormatDisplayName(string formatId);

    /// <summary>
    /// Exports a pack using the specified format.
    /// </summary>
    Task<ExportResult> ExportAsync(Library library, string packName, string formatId, string? outputPath = null);

    /// <summary>
    /// Legacy method for backward compatibility with enum.
    /// </summary>
    Task<ExportResult> ExportAsync(Library library, string packName, ExportFormat format, string? outputPath = null);
}

/// <summary>
/// Legacy enum for backward compatibility.
/// New code should use format IDs from IExportFormatProvider.
/// </summary>
public enum ExportFormat
{
    Json,
    Lua,
    Sql,
    DealershipTs,
    OrganisationTs
}

public class ExportResult
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public string? ErrorMessage { get; set; }
    public int VehicleCount { get; set; }
}

public class ExportService : IExportService
{
    public IReadOnlyList<string> GetAvailableFormats()
    {
        return Customization.ExportFormats.GetFormats().Select(f => f.Id).ToList();
    }

    public string GetFormatDisplayName(string formatId)
    {
        var format = Customization.ExportFormats.GetFormat(formatId);
        return format?.DisplayName ?? formatId;
    }

    public Task<ExportResult> ExportAsync(Library library, string packName, ExportFormat format, string? outputPath = null)
    {
        // Map legacy enum to format ID
        var formatId = format switch
        {
            ExportFormat.Json => "json",
            ExportFormat.Lua => "lua",
            ExportFormat.Sql => "sql",
            ExportFormat.DealershipTs => "dealership_ts",
            ExportFormat.OrganisationTs => "organisation_ts",
            _ => "json"
        };

        return ExportAsync(library, packName, formatId, outputPath);
    }

    public async Task<ExportResult> ExportAsync(Library library, string packName, string formatId, string? outputPath = null)
    {
        if (!library.Packs.TryGetValue(packName, out var pack))
        {
            return new ExportResult
            {
                Success = false,
                ErrorMessage = $"Pack '{packName}' not found."
            };
        }

        var format = Customization.ExportFormats.GetFormat(formatId);
        if (format == null)
        {
            return new ExportResult
            {
                Success = false,
                ErrorMessage = $"Export format '{formatId}' not found."
            };
        }

        // Collect records for export
        var records = new List<Record>();
        foreach (var recordId in pack.Records)
        {
            if (library.Records.TryGetValue(recordId, out var record))
            {
                records.Add(record);
            }
        }

        // Determine output path
        var finalPath = outputPath ?? Path.Combine(
            library.BuildPath,
            $"export_{formatId}_{packName}{format.FileExtension}");

        Directory.CreateDirectory(Path.GetDirectoryName(finalPath)!);

        // Generate and write content
        var content = format.Generate(records, packName);
        await File.WriteAllTextAsync(finalPath, content);

        var vehicleCount = records.Sum(r => r.Variants.Count);

        return new ExportResult
        {
            Success = true,
            OutputPath = finalPath,
            VehicleCount = vehicleCount
        };
    }
}
