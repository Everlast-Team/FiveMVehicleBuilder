using System.Xml.Linq;
using CodeWalker.GameFiles;
using FiveMVehicleBuilder.Core.Models;
using CwRpfFileEntry = CodeWalker.GameFiles.RpfFileEntry;

namespace FiveMVehicleBuilder.Core.Services;

public class RpfService : IRpfService
{
    private readonly List<string> _tempDirectories = [];

    public async Task<RpfAnalysis> AnalyzeAsync(string rpfPath)
    {
        var analysis = new RpfAnalysis { RpfPath = rpfPath };

        if (!File.Exists(rpfPath))
        {
            analysis.Warnings.Add($"Datei existiert nicht: {rpfPath}");
            return analysis;
        }

        try
        {
            // RPF-Datei öffnen - ScanStructure lädt rekursiv alle verschachtelten RPFs
            var rpfFile = new RpfFile(rpfPath, rpfPath);
            await Task.Run(() => rpfFile.ScanStructure(null, null));

            // Alle relevanten Daten sammeln
            var vehiclesMetaEntries = new List<VehicleMetaInfo>();
            var handlingData = new Dictionary<string, RpfFileInfo>(StringComparer.OrdinalIgnoreCase);
            var sirenSettings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var streamFiles = new Dictionary<string, RpfFileInfo>(StringComparer.OrdinalIgnoreCase);
            var carVariationsMetaFiles = new List<RpfFileInfo>();
            var carColsMetaFiles = new List<RpfFileInfo>();

            // Rekursiv durch RPF und verschachtelte RPFs scannen
            await ScanRpfRecursiveAsync(rpfFile, vehiclesMetaEntries, handlingData, sirenSettings, streamFiles, carVariationsMetaFiles, carColsMetaFiles, analysis.Warnings);

            // Fahrzeuge aus gesammelten Daten erstellen
            foreach (var vehicleMeta in vehiclesMetaEntries)
            {
                var modelNameLower = vehicleMeta.ModelName.ToLowerInvariant();

                // Stream-Dateien für dieses Fahrzeug suchen
                var files = new List<RpfFileEntry>();

                // YFT (Pflicht)
                if (streamFiles.TryGetValue($"{modelNameLower}.yft", out var yftInfo))
                {
                    files.Add(CreateRpfFileEntry(yftInfo, RpfFileType.Stream));
                }
                else
                {
                    analysis.Warnings.Add($"Kein .yft für '{vehicleMeta.ModelName}' gefunden");
                    continue;
                }

                // Optional: _hi.yft
                if (streamFiles.TryGetValue($"{modelNameLower}_hi.yft", out var hiYftInfo))
                {
                    files.Add(CreateRpfFileEntry(hiYftInfo, RpfFileType.Stream));
                }

                // Optional: YTD
                if (streamFiles.TryGetValue($"{modelNameLower}.ytd", out var ytdInfo))
                {
                    files.Add(CreateRpfFileEntry(ytdInfo, RpfFileType.Stream));
                }

                // Optional: _hi.ytd
                if (streamFiles.TryGetValue($"{modelNameLower}_hi.ytd", out var hiYtdInfo))
                {
                    files.Add(CreateRpfFileEntry(hiYtdInfo, RpfFileType.Stream));
                }

                // VehicleClass parsen
                int? vehicleClassId = null;
                if (!string.IsNullOrEmpty(vehicleMeta.VehicleClass))
                {
                    if (VehicleConstants.VehicleClassMapping.TryGetValue(vehicleMeta.VehicleClass, out var classId))
                    {
                        vehicleClassId = classId;
                    }
                }

                // Siren-Setting holen
                sirenSettings.TryGetValue(modelNameLower, out var sirenId);

                var vehicle = new RpfVehicleEntry
                {
                    ModelName = vehicleMeta.ModelName,
                    HandlingId = vehicleMeta.HandlingId,
                    VehicleClassName = vehicleMeta.VehicleClass,
                    VehicleClass = vehicleClassId,
                    SirenSettingId = sirenId > 0 ? sirenId : null,
                    Files = files,
                    SourceRpfPath = rpfPath
                };

                // Meta-Dateien hinzufügen (werden beim Extrahieren benötigt)
                if (vehicleMeta.VehiclesMetaInfo != null)
                {
                    files.Add(CreateRpfFileEntry(vehicleMeta.VehiclesMetaInfo, RpfFileType.Meta));
                }

                // Handling.meta hinzufügen (falls vorhanden)
                if (vehicleMeta.HandlingId != null && handlingData.TryGetValue(vehicleMeta.HandlingId, out var handlingInfo))
                {
                    files.Add(CreateRpfFileEntry(handlingInfo, RpfFileType.Meta));
                }

                // CarVariations.meta hinzufügen (alle gefundenen)
                foreach (var carVarInfo in carVariationsMetaFiles)
                {
                    // Nur einmal hinzufügen
                    if (!files.Any(f => f.RelativePath == carVarInfo.Path))
                    {
                        files.Add(CreateRpfFileEntry(carVarInfo, RpfFileType.Meta));
                    }
                }

                // CarCols.meta hinzufügen (alle gefundenen)
                foreach (var carColsInfo in carColsMetaFiles)
                {
                    if (!files.Any(f => f.RelativePath == carColsInfo.Path))
                    {
                        files.Add(CreateRpfFileEntry(carColsInfo, RpfFileType.Meta));
                    }
                }

                analysis.Vehicles.Add(vehicle);
            }
        }
        catch (Exception ex)
        {
            analysis.Warnings.Add($"Fehler beim Lesen der RPF: {ex.Message}");
        }

        return analysis;
    }

    public async Task<string> ExtractToTempAsync(RpfVehicleEntry vehicle)
    {
        // Temporäres Verzeichnis erstellen
        var tempDir = Path.Combine(Path.GetTempPath(), "FiveMVehicleBuilder", Guid.NewGuid().ToString("N"));
        var streamDir = Path.Combine(tempDir, "stream");
        var dataDir = Path.Combine(tempDir, "data");

        Directory.CreateDirectory(streamDir);
        Directory.CreateDirectory(dataDir);
        _tempDirectories.Add(tempDir);

        // RPF öffnen - ScanStructure lädt alle verschachtelten RPFs
        var rpfFile = new RpfFile(vehicle.SourceRpfPath, vehicle.SourceRpfPath);
        await Task.Run(() => rpfFile.ScanStructure(null, null));

        // Alle RPFs (inkl. verschachtelte) in einer flachen Map sammeln
        var allRpfs = new Dictionary<string, RpfFile>(StringComparer.OrdinalIgnoreCase);
        CollectAllRpfs(rpfFile, allRpfs);

        foreach (var file in vehicle.Files)
        {
            try
            {
                // Zielverzeichnis bestimmen
                var targetDir = file.Type == RpfFileType.Meta ? dataDir : streamDir;
                var targetPath = Path.Combine(targetDir, file.Name);

                // Datei aus RPF extrahieren
                var data = await ExtractFileByPathAsync(file.RelativePath, allRpfs);
                if (data != null)
                {
                    await File.WriteAllBytesAsync(targetPath, data);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler beim Extrahieren von {file.Name}: {ex.Message}");
            }
        }

        return tempDir;
    }

    public void CleanupTemp()
    {
        foreach (var dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch
            {
                // Cleanup-Fehler ignorieren
            }
        }
        _tempDirectories.Clear();
    }

    private static RpfFileEntry CreateRpfFileEntry(RpfFileInfo info, RpfFileType type)
    {
        return new RpfFileEntry
        {
            Name = info.Name,
            RelativePath = info.Path,
            Size = info.Size,
            Type = type
        };
    }

    private void CollectAllRpfs(RpfFile rpf, Dictionary<string, RpfFile> allRpfs)
    {
        if (rpf.Path != null)
        {
            allRpfs[rpf.Path] = rpf;
        }

        if (rpf.Children != null)
        {
            foreach (var child in rpf.Children)
            {
                CollectAllRpfs(child, allRpfs);
            }
        }
    }

    private async Task ScanRpfRecursiveAsync(
        RpfFile rpfFile,
        List<VehicleMetaInfo> vehiclesMetaEntries,
        Dictionary<string, RpfFileInfo> handlingData,
        Dictionary<string, int> sirenSettings,
        Dictionary<string, RpfFileInfo> streamFiles,
        List<RpfFileInfo> carVariationsMetaFiles,
        List<RpfFileInfo> carColsMetaFiles,
        List<string> warnings)
    {
        if (rpfFile.AllEntries == null) return;

        foreach (var entry in rpfFile.AllEntries)
        {
            try
            {
                var fileName = entry.Name.ToLowerInvariant();
                var relativePath = entry.Path;

                if (entry is RpfBinaryFileEntry binaryEntry)
                {
                    // Meta-Dateien verarbeiten (inhalts-basierte Erkennung)
                    if (fileName.EndsWith(".meta"))
                    {
                        var data = await Task.Run(() => rpfFile.ExtractFile(binaryEntry));
                        if (data != null)
                        {
                            var metaType = ClassifyMetaFile(data);

                            if (metaType == MetaFileType.Vehicles)
                            {
                                var metaInfo = new RpfFileInfo
                                {
                                    Name = entry.Name,
                                    Path = relativePath,
                                    Size = data.Length,
                                    OwnerRpf = rpfFile
                                };

                                var vehicles = ParseVehiclesMeta(data);
                                foreach (var v in vehicles)
                                {
                                    v.VehiclesMetaInfo = metaInfo;
                                    vehiclesMetaEntries.Add(v);
                                }
                            }
                            else if (metaType == MetaFileType.Handling)
                            {
                                var metaInfo = new RpfFileInfo
                                {
                                    Name = entry.Name,
                                    Path = relativePath,
                                    Size = data.Length,
                                    OwnerRpf = rpfFile
                                };

                                var handlingIds = ParseHandlingMeta(data);
                                foreach (var id in handlingIds)
                                {
                                    handlingData[id] = metaInfo;
                                }
                            }
                            else if (metaType == MetaFileType.CarVariations)
                            {
                                var metaInfo = new RpfFileInfo
                                {
                                    Name = entry.Name,
                                    Path = relativePath,
                                    Size = data.Length,
                                    OwnerRpf = rpfFile
                                };
                                carVariationsMetaFiles.Add(metaInfo);

                                var settings = ParseCarVariationsMeta(data);
                                foreach (var kvp in settings)
                                {
                                    sirenSettings[kvp.Key] = kvp.Value;
                                }
                            }
                            else if (metaType == MetaFileType.CarCols)
                            {
                                var metaInfo = new RpfFileInfo
                                {
                                    Name = entry.Name,
                                    Path = relativePath,
                                    Size = data.Length,
                                    OwnerRpf = rpfFile
                                };
                                carColsMetaFiles.Add(metaInfo);
                            }
                        }
                    }
                    // Stream-Dateien sammeln
                    else if (fileName.EndsWith(".yft") || fileName.EndsWith(".ytd"))
                    {
                        streamFiles[fileName] = new RpfFileInfo
                        {
                            Name = entry.Name,
                            Path = relativePath,
                            Size = binaryEntry.FileUncompressedSize,
                            OwnerRpf = rpfFile
                        };
                    }
                    else if (fileName.EndsWith(".rel") || fileName.EndsWith(".awc"))
                    {
                        streamFiles[fileName] = new RpfFileInfo
                        {
                            Name = entry.Name,
                            Path = relativePath,
                            Size = binaryEntry.FileUncompressedSize,
                            OwnerRpf = rpfFile
                        };
                    }
                }
                else if (entry is RpfResourceFileEntry resourceEntry)
                {
                    // YFT/YTD sind oft Resource-Einträge
                    if (fileName.EndsWith(".yft") || fileName.EndsWith(".ytd"))
                    {
                        streamFiles[fileName] = new RpfFileInfo
                        {
                            Name = entry.Name,
                            Path = relativePath,
                            Size = resourceEntry.FileSize,
                            OwnerRpf = rpfFile
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Fehler beim Verarbeiten von {entry.Name}: {ex.Message}");
            }
        }

        // Verschachtelte RPFs scannen (wurden durch ScanStructure bereits geladen)
        if (rpfFile.Children != null)
        {
            foreach (var childRpf in rpfFile.Children)
            {
                await ScanRpfRecursiveAsync(childRpf, vehiclesMetaEntries, handlingData, sirenSettings, streamFiles, carVariationsMetaFiles, carColsMetaFiles, warnings);
            }
        }
    }

    private async Task<byte[]?> ExtractFileByPathAsync(string relativePath, Dictionary<string, RpfFile> allRpfs)
    {
        // Pfad enthält den RPF-Namen als Teil, z.B. "dlc.rpf\\x64\\levels.rpf\\vehicles.rpf\\police.yft"
        // Finde die RPF die diese Datei enthält
        foreach (var kvp in allRpfs)
        {
            var rpf = kvp.Value;
            if (rpf.AllEntries == null) continue;

            var entry = rpf.AllEntries.FirstOrDefault(e =>
                e.Path != null && e.Path.Equals(relativePath, StringComparison.OrdinalIgnoreCase));

            if (entry != null)
            {
                if (entry is RpfBinaryFileEntry binaryEntry)
                {
                    return await Task.Run(() => rpf.ExtractFile(binaryEntry));
                }
                else if (entry is RpfResourceFileEntry resourceEntry)
                {
                    return await Task.Run(() => rpf.ExtractFile(resourceEntry));
                }
            }
        }

        return null;
    }

    private static List<VehicleMetaInfo> ParseVehiclesMeta(byte[] data)
    {
        var vehicles = new List<VehicleMetaInfo>();

        try
        {
            using var stream = new MemoryStream(data);
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            var doc = XDocument.Parse(content);

            var items = doc.Descendants("Item")
                .Where(x => x.Element("modelName") != null);

            foreach (var item in items)
            {
                var modelName = item.Element("modelName")?.Value;
                if (string.IsNullOrEmpty(modelName))
                    continue;

                vehicles.Add(new VehicleMetaInfo
                {
                    ModelName = modelName,
                    TxdName = item.Element("txdName")?.Value,
                    HandlingId = item.Element("handlingId")?.Value,
                    VehicleClass = item.Element("vehicleClass")?.Value
                });
            }
        }
        catch
        {
            // XML-Parsing-Fehler ignorieren
        }

        return vehicles;
    }

    private static List<string> ParseHandlingMeta(byte[] data)
    {
        var handlingIds = new List<string>();

        try
        {
            using var stream = new MemoryStream(data);
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            var doc = XDocument.Parse(content);

            var items = doc.Descendants("Item")
                .Where(x => x.Element("handlingName") != null);

            foreach (var item in items)
            {
                var handlingName = item.Element("handlingName")?.Value;
                if (!string.IsNullOrEmpty(handlingName))
                {
                    handlingIds.Add(handlingName);
                }
            }
        }
        catch
        {
            // XML-Parsing-Fehler ignorieren
        }

        return handlingIds;
    }

    private static Dictionary<string, int> ParseCarVariationsMeta(byte[] data)
    {
        var sirenSettings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var stream = new MemoryStream(data);
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            var doc = XDocument.Parse(content);

            var items = doc.Descendants("Item")
                .Where(x => x.Element("modelName") != null);

            foreach (var item in items)
            {
                var modelName = item.Element("modelName")?.Value;
                var sirenSettingsValue = item.Element("sirenSettings")?.Attribute("value")?.Value;

                if (!string.IsNullOrEmpty(modelName) && sirenSettingsValue != null)
                {
                    if (int.TryParse(sirenSettingsValue, out var sirenId))
                    {
                        sirenSettings[modelName] = sirenId;
                    }
                }
            }
        }
        catch
        {
            // XML-Parsing-Fehler ignorieren
        }

        return sirenSettings;
    }

    /// <summary>
    /// Interne Struktur für RPF-Datei-Informationen
    /// </summary>
    private class RpfFileInfo
    {
        public required string Name { get; set; }
        public required string Path { get; set; }
        public long Size { get; set; }
        public required RpfFile OwnerRpf { get; set; }
    }

    private class VehicleMetaInfo
    {
        public required string ModelName { get; set; }
        public string? TxdName { get; set; }
        public string? HandlingId { get; set; }
        public string? VehicleClass { get; set; }
        public RpfFileInfo? VehiclesMetaInfo { get; set; }
    }

    private enum MetaFileType
    {
        Unknown,
        Vehicles,
        Handling,
        CarVariations,
        CarCols
    }

    /// <summary>
    /// Klassifiziert eine Meta-Datei anhand ihres XML-Inhalts
    /// </summary>
    private static MetaFileType ClassifyMetaFile(byte[] data)
    {
        try
        {
            using var stream = new MemoryStream(data);
            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();

            XDocument? doc = null;
            try
            {
                doc = XDocument.Parse(content);
            }
            catch
            {
                return MetaFileType.Unknown;
            }

            var root = doc.Root?.Name.LocalName;

            // vehicles.meta: Root ist CVehicleModelInfo__InitDataList oder enthält modelName + handlingId
            if (root == "CVehicleModelInfo__InitDataList" ||
                doc.Descendants("modelName").Any(e => e.Parent?.Element("handlingId") != null))
            {
                return MetaFileType.Vehicles;
            }

            // handling.meta: Root ist CHandlingDataMgr oder enthält handlingName
            if (root == "CHandlingDataMgr" ||
                doc.Descendants("handlingName").Any() ||
                doc.Descendants("Item").Any(e => e.Attribute("type")?.Value == "CHandlingData"))
            {
                return MetaFileType.Handling;
            }

            // carvariations.meta: Root ist CVehicleModelInfoVariation oder enthält sirenSettings
            if (root == "CVehicleModelInfoVariation" ||
                doc.Descendants("sirenSettings").Any())
            {
                return MetaFileType.CarVariations;
            }

            // carcols.meta: Root ist CVehicleModelInfoVarGlobal oder enthält Sirens/lights
            if (root == "CVehicleModelInfoVarGlobal" ||
                doc.Descendants("Sirens").Any() ||
                doc.Descendants("lightSettings").Any())
            {
                return MetaFileType.CarCols;
            }
        }
        catch
        {
            // Fehler ignorieren
        }

        return MetaFileType.Unknown;
    }
}
