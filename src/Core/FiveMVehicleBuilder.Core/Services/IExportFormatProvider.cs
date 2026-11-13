using FiveMVehicleBuilder.Core.Models;

namespace FiveMVehicleBuilder.Core.Services;

/// <summary>
/// Represents an export format for vehicle data.
/// </summary>
public interface IExportFormat
{
    /// <summary>
    /// Unique identifier for the format (e.g., "json", "lua").
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Display name shown in UI.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// File extension including dot (e.g., ".json", ".lua").
    /// </summary>
    string FileExtension { get; }

    /// <summary>
    /// Generates export content for the given records.
    /// </summary>
    string Generate(IEnumerable<Record> records, string packName);
}

/// <summary>
/// Provides available export formats.
/// Implement this interface to add custom export formats.
/// </summary>
public interface IExportFormatProvider
{
    /// <summary>
    /// Gets all available export formats.
    /// </summary>
    IReadOnlyList<IExportFormat> GetFormats();

    /// <summary>
    /// Gets a specific format by ID.
    /// </summary>
    IExportFormat? GetFormat(string id);
}

/// <summary>
/// Default implementation with basic export formats (JSON, Lua, SQL).
/// </summary>
public class DefaultExportFormatProvider : IExportFormatProvider
{
    private readonly List<IExportFormat> _formats;

    public DefaultExportFormatProvider()
    {
        _formats =
        [
            new JsonExportFormat(),
            new LuaExportFormat(),
            new SqlExportFormat()
        ];
    }

    public virtual IReadOnlyList<IExportFormat> GetFormats() => _formats;

    public virtual IExportFormat? GetFormat(string id) =>
        _formats.FirstOrDefault(f => f.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Adds a custom format. Call this in derived classes to register additional formats.
    /// </summary>
    protected void AddFormat(IExportFormat format) => _formats.Add(format);
}

#region Built-in Export Formats

public class JsonExportFormat : IExportFormat
{
    public string Id => "json";
    public string DisplayName => "JSON";
    public string FileExtension => ".json";

    public string Generate(IEnumerable<Record> records, string packName)
    {
        var vehicles = records.SelectMany(r => r.Variants.Select(v => new
        {
            model = v.Name,
            name = r.OriginalName.ToUpperInvariant(),
            category = r.Category,
            price = r.Price,
            liveries = v.Liveries.ToDictionary(l => l.Index, l => l.Department)
        })).ToList();

        return System.Text.Json.JsonSerializer.Serialize(vehicles, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }
}

public class LuaExportFormat : IExportFormat
{
    public string Id => "lua";
    public string DisplayName => "Lua";
    public string FileExtension => ".lua";

    public string Generate(IEnumerable<Record> records, string packName)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("local Vehicles = {");

        foreach (var record in records)
        {
            foreach (var variant in record.Variants)
            {
                sb.AppendLine($"    {{");
                sb.AppendLine($"        model = \"{variant.Name}\",");
                sb.AppendLine($"        name = \"{record.OriginalName.ToUpperInvariant()}\",");
                sb.AppendLine($"        category = \"{record.Category}\",");
                if (record.Price.HasValue)
                    sb.AppendLine($"        price = {record.Price.Value},");

                if (variant.Liveries.Count > 0)
                {
                    sb.AppendLine($"        liveries = {{");
                    foreach (var livery in variant.Liveries)
                    {
                        sb.AppendLine($"            [{livery.Index}] = \"{livery.Department}\",");
                    }
                    sb.AppendLine($"        }},");
                }

                sb.AppendLine($"    }},");
            }
        }

        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("return Vehicles");

        return sb.ToString();
    }
}

public class SqlExportFormat : IExportFormat
{
    public string Id => "sql";
    public string DisplayName => "SQL";
    public string FileExtension => ".sql";

    public string Generate(IEnumerable<Record> records, string packName)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("-- Vehicle Export");
        sb.AppendLine($"-- Pack: {packName}");
        sb.AppendLine($"-- Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("INSERT INTO vehicles (spawn_name, display_name, category, vehicle_class, price) VALUES");

        var values = new List<string>();
        foreach (var record in records)
        {
            foreach (var variant in record.Variants)
            {
                var displayName = record.OriginalName.ToUpperInvariant().Replace("'", "''");
                var vehicleClass = record.VehicleClass.HasValue ? record.VehicleClass.Value.ToString() : "NULL";
                var price = record.Price.HasValue ? record.Price.Value.ToString() : "NULL";
                values.Add($"('{variant.Name}', '{displayName}', '{record.Category}', {vehicleClass}, {price})");
            }
        }

        sb.AppendLine(string.Join(",\n", values) + ";");

        return sb.ToString();
    }
}

#endregion
