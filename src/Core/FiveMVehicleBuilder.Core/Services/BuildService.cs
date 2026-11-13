using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using FiveMVehicleBuilder.Core.Models;

namespace FiveMVehicleBuilder.Core.Services;

public class BuildService : IBuildService
{
    private readonly IXmlValidationService _xmlValidationService;

    public BuildService(IXmlValidationService xmlValidationService)
    {
        _xmlValidationService = xmlValidationService;
    }

    public async Task<BuildResult> BuildPackAsync(Library library, string packName, IProgress<BuildProgress>? progress = null)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!library.Packs.TryGetValue(packName, out var pack))
        {
            return new BuildResult
            {
                Success = false,
                ErrorMessage = $"Pack '{packName}' nicht gefunden"
            };
        }

        // Records sammeln
        var records = new List<Record>();
        foreach (var recordId in pack.Records)
        {
            if (!library.Records.TryGetValue(recordId, out var record))
            {
                return new BuildResult
                {
                    Success = false,
                    ErrorMessage = $"Record '{recordId}' nicht gefunden"
                };
            }
            records.Add(record);
        }

        // Pre-Build XML-Validierung
        progress?.Report(new BuildProgress
        {
            Stage = "Validiere Meta-Dateien",
            CurrentItem = "XML-Prüfung",
            Current = 0,
            Total = records.Count
        });

        var validationResult = await ValidateRecordsMetaFilesAsync(library, records, progress);
        if (validationResult.HasErrors)
        {
            return new BuildResult
            {
                Success = false,
                ErrorMessage = "XML-Validierung fehlgeschlagen",
                CheckResult = validationResult
            };
        }

        // Build-Verzeichnis vorbereiten
        var outputPath = Path.Combine(library.BuildPath, pack.ResourceName);
        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, recursive: true);
        }

        var streamPath = Path.Combine(outputPath, "stream");
        var dataPath = Path.Combine(outputPath, "data");
        Directory.CreateDirectory(streamPath);
        Directory.CreateDirectory(dataPath);

        // Varianten zählen
        var totalVariants = records.Sum(r => r.Variants.Count);
        var currentVariant = 0;
        long totalSize = 0;

        // Jede Variante bauen
        foreach (var record in records)
        {
            var recordOriginalPath = Path.Combine(library.RecordsPath, record.Id, "original");

            foreach (var variant in record.Variants)
            {
                currentVariant++;
                progress?.Report(new BuildProgress
                {
                    Stage = "Baue Varianten",
                    CurrentItem = variant.Name,
                    Current = currentVariant,
                    Total = totalVariants
                });

                // Stream-Dateien kopieren und umbenennen
                var variantStreamPath = Path.Combine(streamPath, variant.Name);
                Directory.CreateDirectory(variantStreamPath);

                totalSize += await CopyAndRenameStreamFilesAsync(
                    recordOriginalPath,
                    variantStreamPath,
                    record.OriginalName,
                    variant.Name);

                // Meta-Dateien generieren
                var variantDataPath = Path.Combine(dataPath, variant.Name);
                Directory.CreateDirectory(variantDataPath);

                await GenerateMetaFilesAsync(
                    recordOriginalPath,
                    variantDataPath,
                    record,
                    variant);
            }
        }

        // fxmanifest.lua generieren
        progress?.Report(new BuildProgress
        {
            Stage = "Generiere Manifest",
            CurrentItem = "fxmanifest.lua",
            Current = totalVariants,
            Total = totalVariants
        });

        await GenerateFxManifestAsync(outputPath, pack.ResourceName);

        stopwatch.Stop();

        return new BuildResult
        {
            Success = true,
            OutputPath = outputPath,
            Statistics = new BuildStatistics
            {
                RecordCount = records.Count,
                VariantCount = totalVariants,
                TotalSizeBytes = totalSize,
                Duration = stopwatch.Elapsed
            }
        };
    }

    public async Task<BuildResult> BuildTestAsync(Library library, string recordId, IProgress<BuildProgress>? progress = null)
    {
        if (!library.Records.TryGetValue(recordId, out var record))
        {
            return new BuildResult
            {
                Success = false,
                ErrorMessage = $"Record '{recordId}' nicht gefunden"
            };
        }

        // Temporäres Pack erstellen
        var testPack = new Pack
        {
            Name = $"test_{recordId}",
            ResourceName = $"test_{recordId}",
            Records = [recordId]
        };

        // Temporär zur Library hinzufügen
        library.Packs[testPack.Name] = testPack;

        try
        {
            return await BuildPackAsync(library, testPack.Name, progress);
        }
        finally
        {
            // Wieder entfernen
            library.Packs.Remove(testPack.Name);
        }
    }

    private static async Task<long> CopyAndRenameStreamFilesAsync(
        string sourcePath,
        string destPath,
        string originalName,
        string newName)
    {
        long totalSize = 0;
        var sourceStreamPath = Path.Combine(sourcePath, "stream");

        if (!Directory.Exists(sourceStreamPath))
            return 0;

        var originalNameLower = originalName.ToLowerInvariant();

        // Hauptdateien die umbenannt werden müssen
        var mainFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            $"{originalNameLower}.yft",
            $"{originalNameLower}_hi.yft",
            $"{originalNameLower}.ytd",
            $"{originalNameLower}_hi.ytd"
        };

        foreach (var file in Directory.GetFiles(sourceStreamPath))
        {
            var fileName = Path.GetFileName(file);
            var fileNameLower = fileName.ToLowerInvariant();
            string destFileName;

            if (mainFiles.Contains(fileNameLower))
            {
                // Hauptdatei umbenennen (originalName -> newName)
                destFileName = fileName.ToLowerInvariant().Replace(originalNameLower, newName);
            }
            else
            {
                // Anbauteil: unverändert kopieren
                destFileName = fileName;
            }

            var destFile = Path.Combine(destPath, destFileName);
            File.Copy(file, destFile);
            totalSize += new FileInfo(destFile).Length;
        }

        return await Task.FromResult(totalSize);
    }

    private static async Task GenerateMetaFilesAsync(
        string sourcePath,
        string destPath,
        Record record,
        Variant variant)
    {
        var sourceDataPath = Path.Combine(sourcePath, "data");

        // vehicles.meta
        var vehiclesMetaSource = Path.Combine(sourceDataPath, "vehicles.meta");
        if (File.Exists(vehiclesMetaSource))
        {
            var vehiclesMeta = await TransformVehiclesMetaAsync(
                vehiclesMetaSource,
                record.OriginalName,
                variant.Name,
                record.HandlingId);
            await File.WriteAllTextAsync(Path.Combine(destPath, "vehicles.meta"), vehiclesMeta);
        }

        // handling.meta
        var handlingMetaSource = Path.Combine(sourceDataPath, "handling.meta");
        if (File.Exists(handlingMetaSource))
        {
            var handlingMeta = await TransformHandlingMetaAsync(
                handlingMetaSource,
                record.OriginalName,
                variant.Name,
                record.HandlingId);
            await File.WriteAllTextAsync(Path.Combine(destPath, "handling.meta"), handlingMeta);
        }

        // carvariations.meta
        var carVariationsMetaSource = Path.Combine(sourceDataPath, "carvariations.meta");
        if (File.Exists(carVariationsMetaSource))
        {
            var carVariationsMeta = await TransformCarVariationsMetaAsync(
                carVariationsMetaSource,
                record.OriginalName,
                variant.Name,
                variant.SirenSettingId);
            await File.WriteAllTextAsync(Path.Combine(destPath, "carvariations.meta"), carVariationsMeta);
        }
    }

    private static async Task<string> TransformVehiclesMetaAsync(
        string sourcePath,
        string originalName,
        string newName,
        string handlingId)
    {
        var content = await File.ReadAllTextAsync(sourcePath);
        var doc = XDocument.Parse(content);

        foreach (var item in doc.Descendants("Item"))
        {
            var modelNameElement = item.Element("modelName");
            if (modelNameElement?.Value.Equals(originalName, StringComparison.OrdinalIgnoreCase) == true)
            {
                modelNameElement.Value = newName;

                // txdName auch umbenennen
                var txdNameElement = item.Element("txdName");
                if (txdNameElement != null && txdNameElement.Value.Equals(originalName, StringComparison.OrdinalIgnoreCase))
                {
                    txdNameElement.Value = newName;
                }

                // gameName umbenennen
                var gameNameElement = item.Element("gameName");
                if (gameNameElement != null && gameNameElement.Value.Equals(originalName, StringComparison.OrdinalIgnoreCase))
                {
                    gameNameElement.Value = newName;
                }

                // handlingId setzen
                var handlingIdElement = item.Element("handlingId");
                if (handlingIdElement != null)
                {
                    handlingIdElement.Value = handlingId.ToUpperInvariant();
                }
            }
        }

        // txdRelationships aktualisieren
        foreach (var item in doc.Descendants("txdRelationships").Descendants("Item"))
        {
            var parent = item.Element("parent");
            var child = item.Element("child");

            if (parent?.Value.Equals(originalName, StringComparison.OrdinalIgnoreCase) == true)
            {
                parent.Value = newName;
            }
            if (child?.Value.Equals(originalName, StringComparison.OrdinalIgnoreCase) == true)
            {
                child.Value = newName;
            }
        }

        return doc.Declaration?.ToString() + Environment.NewLine + doc.ToString();
    }

    private static async Task<string> TransformHandlingMetaAsync(
        string sourcePath,
        string originalName,
        string newName,
        string handlingId)
    {
        var content = await File.ReadAllTextAsync(sourcePath);
        var doc = XDocument.Parse(content);

        foreach (var item in doc.Descendants("Item"))
        {
            var handlingNameElement = item.Element("handlingName");
            if (handlingNameElement != null)
            {
                // Original-HandlingId durch neue ersetzen
                handlingNameElement.Value = handlingId.ToUpperInvariant();
            }
        }

        return doc.Declaration?.ToString() + Environment.NewLine + doc.ToString();
    }

    private static async Task<string> TransformCarVariationsMetaAsync(
        string sourcePath,
        string originalName,
        string newName,
        int? sirenSettingId)
    {
        var content = await File.ReadAllTextAsync(sourcePath);
        var doc = XDocument.Parse(content);

        foreach (var item in doc.Descendants("variationData").Descendants("Item"))
        {
            var modelNameElement = item.Element("modelName");
            if (modelNameElement?.Value.Equals(originalName, StringComparison.OrdinalIgnoreCase) == true)
            {
                modelNameElement.Value = newName;

                // sirenSettings aktualisieren
                if (sirenSettingId.HasValue)
                {
                    var sirenSettingsElement = item.Element("sirenSettings");
                    if (sirenSettingsElement != null)
                    {
                        sirenSettingsElement.SetAttributeValue("value", sirenSettingId.Value);
                    }
                }
            }
        }

        return doc.Declaration?.ToString() + Environment.NewLine + doc.ToString();
    }

    private static async Task GenerateFxManifestAsync(string outputPath, string resourceName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("fx_version 'cerulean'");
        sb.AppendLine("game 'gta5'");
        sb.AppendLine();
        sb.AppendLine($"author 'FiveMVehicleBuilder'");
        sb.AppendLine($"description '{resourceName}'");
        sb.AppendLine($"version '1.0.0'");
        sb.AppendLine();
        sb.AppendLine("files {");
        sb.AppendLine("    'data/**/vehicles.meta',");
        sb.AppendLine("    'data/**/handling.meta',");
        sb.AppendLine("    'data/**/carvariations.meta',");
        sb.AppendLine("    'data/**/carcols.meta'");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("data_file 'HANDLING_FILE' 'data/**/handling.meta'");
        sb.AppendLine("data_file 'VEHICLE_METADATA_FILE' 'data/**/vehicles.meta'");
        sb.AppendLine("data_file 'CARVARIATIONS_FILE' 'data/**/carvariations.meta'");
        sb.AppendLine("data_file 'VEHICLE_LAYOUTS_FILE' 'data/**/carcols.meta'");

        await File.WriteAllTextAsync(Path.Combine(outputPath, "fxmanifest.lua"), sb.ToString());
    }

    private async Task<CheckResult> ValidateRecordsMetaFilesAsync(
        Library library,
        List<Record> records,
        IProgress<BuildProgress>? progress)
    {
        var result = new CheckResult();
        var current = 0;

        foreach (var record in records)
        {
            current++;
            progress?.Report(new BuildProgress
            {
                Stage = "Validiere Meta-Dateien",
                CurrentItem = record.Id,
                Current = current,
                Total = records.Count
            });

            var dataPath = Path.Combine(library.RecordsPath, record.Id, "original", "data");

            if (!Directory.Exists(dataPath))
                continue;

            var metaFiles = new[] { "vehicles.meta", "handling.meta", "carvariations.meta", "carcols.meta" };

            foreach (var metaFile in metaFiles)
            {
                var filePath = Path.Combine(dataPath, metaFile);
                if (!File.Exists(filePath))
                    continue;

                var validation = await _xmlValidationService.ValidateXmlFileAsync(filePath);
                if (!validation.IsValid)
                {
                    // Versuch zu reparieren
                    var content = await File.ReadAllTextAsync(filePath);
                    var repaired = _xmlValidationService.TryRepairXml(content);

                    if (repaired != null)
                    {
                        // Reparatur erfolgreich - Datei aktualisieren
                        await File.WriteAllTextAsync(filePath, repaired);
                    }
                    else
                    {
                        // Nicht reparierbar - Fehler melden
                        result.Findings.Add(new CheckFinding
                        {
                            Severity = CheckSeverity.Error,
                            Code = "INVALID_XML",
                            Message = validation.FormattedError,
                            RecordId = record.Id,
                            FilePath = filePath
                        });
                    }
                }
            }
        }

        return result;
    }
}
