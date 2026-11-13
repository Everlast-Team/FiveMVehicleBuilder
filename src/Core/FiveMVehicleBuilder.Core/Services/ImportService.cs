using System.Xml.Linq;
using FiveMVehicleBuilder.Core.Models;

namespace FiveMVehicleBuilder.Core.Services;

public class ImportService : IImportService
{
    public async Task<ImportAnalysis> AnalyzeAsync(string resourcePath)
    {
        var analysis = new ImportAnalysis { ResourcePath = resourcePath };

        // Prüfen ob Pfad existiert
        if (!Directory.Exists(resourcePath))
        {
            analysis.Warnings.Add($"Verzeichnis existiert nicht: {resourcePath}");
            return analysis;
        }

        // fxmanifest.lua prüfen (nur Warnung, kein Abbruch)
        var manifestPath = Path.Combine(resourcePath, "fxmanifest.lua");
        if (!File.Exists(manifestPath))
        {
            manifestPath = Path.Combine(resourcePath, "__resource.lua");
            if (!File.Exists(manifestPath))
            {
                analysis.Warnings.Add("Kein fxmanifest.lua oder __resource.lua gefunden");
            }
        }

        // Stream-Dateien finden
        var streamFiles = FindStreamFiles(resourcePath);

        // Alle .meta Dateien finden und nach Inhalt klassifizieren
        var allMetaFiles = FindAllMetaFiles(resourcePath);
        var classifiedMeta = await ClassifyMetaFilesAsync(allMetaFiles);

        var vehiclesMetaPaths = classifiedMeta.VehiclesMeta;
        var hasVehiclesMeta = vehiclesMetaPaths.Count > 0;

        // Wenn keine Stream-Dateien und keine vehicles.meta -> nichts zu tun
        if (streamFiles.Count == 0 && !hasVehiclesMeta)
        {
            analysis.Warnings.Add("Keine Stream-Dateien (.yft, .ytd) und keine vehicles.meta gefunden");
            return analysis;
        }

        if (!hasVehiclesMeta && streamFiles.Count > 0)
        {
            analysis.Warnings.Add("Keine vehicles.meta gefunden - Fahrzeuge werden aus Dateinamen erkannt, Meta-Dateien werden generiert");
        }

        // handling.meta verarbeiten (bereits klassifiziert)
        var handlingData = new Dictionary<string, string>(); // handlingId -> path
        foreach (var handlingPath in classifiedMeta.HandlingMeta)
        {
            var handlingIds = await ParseHandlingMetaAsync(handlingPath);
            foreach (var id in handlingIds)
            {
                handlingData[id.ToUpperInvariant()] = handlingPath;
            }
        }

        // carvariations.meta verarbeiten (bereits klassifiziert)
        var sirenSettings = new Dictionary<string, int>(); // modelName -> sirenSettingId
        foreach (var carVarPath in classifiedMeta.CarVariationsMeta)
        {
            var settings = await ParseCarVariationsMetaAsync(carVarPath);
            foreach (var kvp in settings)
            {
                sirenSettings[kvp.Key.ToLowerInvariant()] = kvp.Value;
            }
        }

        // Fahrzeuge sammeln - entweder aus vehicles.meta oder aus .yft Dateien
        var allVehicleEntries = new List<(VehicleMetaEntry vehicle, string? metaPath)>();

        if (hasVehiclesMeta)
        {
            // Aus vehicles.meta parsen
            foreach (var vehiclesMetaPath in vehiclesMetaPaths)
            {
                var vehicles = await ParseVehiclesMetaAsync(vehiclesMetaPath);
                foreach (var vehicle in vehicles)
                {
                    allVehicleEntries.Add((vehicle, vehiclesMetaPath));
                }
            }
        }

        // Wenn keine vehicles.meta, aber Stream-Dateien vorhanden: aus .yft Dateinamen ableiten
        if (!hasVehiclesMeta && streamFiles.Count > 0)
        {
            var yftFiles = streamFiles
                .Where(kvp => kvp.Key.EndsWith(".yft") && !kvp.Key.EndsWith("_hi.yft"))
                .ToList();

            foreach (var kvp in yftFiles)
            {
                var modelName = Path.GetFileNameWithoutExtension(kvp.Key);
                allVehicleEntries.Add((new VehicleMetaEntry
                {
                    ModelName = modelName,
                    TxdName = null,
                    HandlingId = null,
                    VehicleClass = null
                }, null));
            }
        }

        if (allVehicleEntries.Count == 0)
        {
            analysis.Warnings.Add("Keine Fahrzeuge gefunden");
            return analysis;
        }

        // Alle Hauptdateien aller Fahrzeuge sammeln (um sie von Anbauteilen zu unterscheiden)
        var allMainFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (vehicle, _) in allVehicleEntries)
        {
            var modelNameLower = vehicle.ModelName.ToLowerInvariant();
            allMainFiles.Add($"{modelNameLower}.yft");
            allMainFiles.Add($"{modelNameLower}_hi.yft");
            allMainFiles.Add($"{modelNameLower}.ytd");
            allMainFiles.Add($"{modelNameLower}_hi.ytd");
        }

        // Anbauteile sind alle Dateien die zu keinem Fahrzeug gehören
        var attachmentFiles = streamFiles
            .Where(kvp => !allMainFiles.Contains(kvp.Key))
            .Select(kvp => kvp.Value)
            .ToList();

        // Jetzt Fahrzeuge verarbeiten
        foreach (var (vehicle, vehiclesMetaPath) in allVehicleEntries)
        {
            var modelNameLower = vehicle.ModelName.ToLowerInvariant();

            // Stream-Dateien zuordnen
            var yftPath = streamFiles.GetValueOrDefault($"{modelNameLower}.yft");
            var hiYftPath = streamFiles.GetValueOrDefault($"{modelNameLower}_hi.yft");
            var ytdPath = streamFiles.GetValueOrDefault($"{modelNameLower}.ytd");
            var hiYtdPath = streamFiles.GetValueOrDefault($"{modelNameLower}_hi.ytd");

            // Prüfen ob .yft existiert
            if (yftPath == null)
            {
                analysis.SkippedVehicles.Add(new SkippedVehicle
                {
                    ModelName = vehicle.ModelName,
                    Reason = $"Keine .yft-Datei gefunden für '{vehicle.ModelName}'"
                });
                continue;
            }

            // txdRelationships prüfen (gemeinsames Parent-ytd wird nicht unterstützt)
            if (vehicle.TxdName != null &&
                !vehicle.TxdName.Equals(vehicle.ModelName, StringComparison.OrdinalIgnoreCase) &&
                !vehicle.TxdName.Equals("vehshare", StringComparison.OrdinalIgnoreCase))
            {
                analysis.SkippedVehicles.Add(new SkippedVehicle
                {
                    ModelName = vehicle.ModelName,
                    Reason = $"Fahrzeug nutzt gemeinsames Parent-ytd '{vehicle.TxdName}' (nicht unterstützt)"
                });
                continue;
            }

            // Bei nur einem Fahrzeug: alle Anbauteile zuordnen
            // Bei mehreren Fahrzeugen: keine Anbauteile (unklar welchem sie gehören)
            var vehicleAttachments = allVehicleEntries.Count == 1 ? attachmentFiles : [];

            // VehicleClass parsen
            int? vehicleClassId = null;
            if (!string.IsNullOrEmpty(vehicle.VehicleClass))
            {
                if (Models.VehicleConstants.VehicleClassMapping.TryGetValue(vehicle.VehicleClass, out var classId))
                {
                    vehicleClassId = classId;
                }
            }

            var analyzedVehicle = new AnalyzedVehicle
            {
                ModelName = vehicle.ModelName,
                StreamFiles = new VehicleStreamFiles
                {
                    YftPath = yftPath,
                    HiYftPath = hiYftPath,
                    YtdPath = ytdPath,
                    HiYtdPath = hiYtdPath,
                    AdditionalFiles = vehicleAttachments
                },
                VehiclesMetaPath = vehiclesMetaPath, // Kann null sein!
                HandlingId = vehicle.HandlingId,
                HandlingMetaPath = vehicle.HandlingId != null
                    ? handlingData.GetValueOrDefault(vehicle.HandlingId.ToUpperInvariant())
                    : null,
                CarVariationsMetaPath = classifiedMeta.CarVariationsMeta.FirstOrDefault(),
                CarColsMetaPath = classifiedMeta.CarColsMeta.FirstOrDefault(),
                SirenSettingId = sirenSettings.GetValueOrDefault(modelNameLower),
                VehicleClassName = vehicle.VehicleClass,
                VehicleClass = vehicleClassId
            };

            // Siren-Setting von 0 auf null setzen (0 = kein Setting)
            if (analyzedVehicle.SirenSettingId == 0)
            {
                analyzedVehicle.SirenSettingId = null;
            }

            analysis.Vehicles.Add(analyzedVehicle);

            // Stream-Dateien als zugeordnet markieren
            streamFiles.Remove($"{modelNameLower}.yft");
            if (hiYftPath != null) streamFiles.Remove($"{modelNameLower}_hi.yft");
            if (ytdPath != null) streamFiles.Remove($"{modelNameLower}.ytd");
            if (hiYtdPath != null) streamFiles.Remove($"{modelNameLower}_hi.ytd");
        }

        // Bei einem Fahrzeug: Anbauteile wurden zugeordnet, aus orphaned entfernen
        if (allVehicleEntries.Count == 1)
        {
            foreach (var attachment in attachmentFiles)
            {
                var fileName = Path.GetFileName(attachment).ToLowerInvariant();
                streamFiles.Remove(fileName);
            }
        }

        // Nicht zugeordnete Stream-Dateien als verwaist markieren
        analysis.OrphanedFiles.AddRange(streamFiles.Values);

        return analysis;
    }

    public Task<Record> ImportAsync(Library library, AnalyzedVehicle vehicle, string category, string? source = null, string? customName = null)
    {
        // Basisnamen generieren oder benutzerdefinierten Namen verwenden
        var baseName = string.IsNullOrWhiteSpace(customName)
            ? GenerateBaseName(library, category)
            : customName.Trim().ToLowerInvariant();

        // Record-Verzeichnis erstellen
        var recordPath = Path.Combine(library.RecordsPath, baseName);
        var originalPath = Path.Combine(recordPath, "original");
        var streamPath = Path.Combine(originalPath, "stream");
        var dataPath = Path.Combine(originalPath, "data");

        Directory.CreateDirectory(streamPath);
        Directory.CreateDirectory(dataPath);

        // Stream-Dateien kopieren
        File.Copy(vehicle.StreamFiles.YftPath, Path.Combine(streamPath, Path.GetFileName(vehicle.StreamFiles.YftPath)));
        if (vehicle.StreamFiles.HiYftPath != null)
        {
            File.Copy(vehicle.StreamFiles.HiYftPath, Path.Combine(streamPath, Path.GetFileName(vehicle.StreamFiles.HiYftPath)));
        }
        if (vehicle.StreamFiles.YtdPath != null)
        {
            File.Copy(vehicle.StreamFiles.YtdPath, Path.Combine(streamPath, Path.GetFileName(vehicle.StreamFiles.YtdPath)));
        }
        if (vehicle.StreamFiles.HiYtdPath != null)
        {
            File.Copy(vehicle.StreamFiles.HiYtdPath, Path.Combine(streamPath, Path.GetFileName(vehicle.StreamFiles.HiYtdPath)));
        }
        // Anbau-Elemente kopieren
        foreach (var additionalFile in vehicle.StreamFiles.AdditionalFiles)
        {
            File.Copy(additionalFile, Path.Combine(streamPath, Path.GetFileName(additionalFile)));
        }

        // Meta-Dateien kopieren oder generieren
        if (vehicle.VehiclesMetaPath != null)
        {
            File.Copy(vehicle.VehiclesMetaPath, Path.Combine(dataPath, "vehicles.meta"));
        }
        else
        {
            // Template vehicles.meta generieren
            var vehiclesMeta = GenerateVehiclesMeta(vehicle.ModelName, baseName.ToUpperInvariant());
            File.WriteAllText(Path.Combine(dataPath, "vehicles.meta"), vehiclesMeta);
        }

        if (vehicle.HandlingMetaPath != null)
        {
            File.Copy(vehicle.HandlingMetaPath, Path.Combine(dataPath, "handling.meta"));
        }
        else if (vehicle.NeedsMetaGeneration)
        {
            // Template handling.meta generieren
            var handlingMeta = GenerateHandlingMeta(baseName.ToUpperInvariant());
            File.WriteAllText(Path.Combine(dataPath, "handling.meta"), handlingMeta);
        }

        if (vehicle.CarVariationsMetaPath != null)
        {
            File.Copy(vehicle.CarVariationsMetaPath, Path.Combine(dataPath, "carvariations.meta"));
        }

        if (vehicle.CarColsMetaPath != null)
        {
            File.Copy(vehicle.CarColsMetaPath, Path.Combine(dataPath, "carcols.meta"));
        }

        // Erste Variante erstellen
        var variantName = $"{baseName}a";
        var variant = new Variant
        {
            Name = variantName,
            SirenSettingId = vehicle.SirenSettingId
        };

        // Record erstellen
        var record = new Record
        {
            Id = baseName,
            OriginalName = vehicle.ModelName,
            Source = source,
            ImportedAt = DateOnly.FromDateTime(DateTime.Now),
            Category = category,
            HandlingId = vehicle.HandlingId?.ToUpperInvariant() ?? baseName.ToUpperInvariant(),
            VehicleClass = vehicle.VehicleClass,
            Variants = [variant]
        };

        // In Bibliothek speichern
        library.Records[record.Id] = record;

        // Nummer für nächsten Import erhöhen
        if (library.Settings.Prefixes.TryGetValue(category, out var prefixSettings))
        {
            prefixSettings.NextNumber++;
        }

        return Task.FromResult(record);
    }

    private static string GenerateVehiclesMeta(string modelName, string handlingId)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<CVehicleModelInfo__InitDataList>
  <residentTxd>vehshare</residentTxd>
  <residentAnims />
  <InitDatas>
    <Item>
      <modelName>{modelName}</modelName>
      <txdName>{modelName}</txdName>
      <handlingId>{handlingId}</handlingId>
      <gameName>{modelName}</gameName>
      <vehicleMakeName />
      <expressionDictName>null</expressionDictName>
      <expressionName>null</expressionName>
      <animConvRoofDictName>null</animConvRoofDictName>
      <animConvRoofName>null</animConvRoofName>
      <animConvRoofWindowsAffected />
      <ptfxAssetName>null</ptfxAssetName>
      <audioNameHash />
      <layout>LAYOUT_STANDARD</layout>
      <coverBoundOffsets>YOURMODSNAME_YOURMODELNAME</coverBoundOffsets>
      <explosionInfo>YOURMODSNAME_YOURMODELNAME</explosionInfo>
      <scenarioLayout />
      <cameraName>DEFAULT_FOLLOW_VEHICLE_CAMERA</cameraName>
      <aimCameraName>YOURMODSNAME_YOURMODELNAME</aimCameraName>
      <bonnetCameraName>YOURMODSNAME_YOURMODELNAME</bonnetCameraName>
      <povCameraName>DEFAULT_POV_CAMERA</povCameraName>
      <FirstPersonDriveByIKOffset x=""0.000000"" y=""0.000000"" z=""0.000000"" />
      <FirstPersonDriveByUnarmedIKOffset x=""0.000000"" y=""0.000000"" z=""0.000000"" />
      <FirstPersonProjectileDriveByIKOffset x=""0.000000"" y=""0.000000"" z=""0.000000"" />
      <FirstPersonProjectileDriveByPassengerIKOffset x=""0.000000"" y=""0.000000"" z=""0.000000"" />
      <FirstPersonDriveByRightPassengerIKOffset x=""0.000000"" y=""0.000000"" z=""0.000000"" />
      <FirstPersonDriveByRightPassengerUnarmedIKOffset x=""0.000000"" y=""0.000000"" z=""0.000000"" />
      <FirstPersonMobilePhoneOffset x=""0.000000"" y=""0.000000"" z=""0.000000"" />
      <FirstPersonPassengerMobilePhoneOffset x=""0.000000"" y=""0.000000"" z=""0.000000"" />
      <PovCameraOffset x=""0.000000"" y=""0.000000"" z=""0.000000"" />
      <PovCameraVerticalAdjustmentForRollCage value=""0.000000"" />
      <PovPassengerCameraOffset x=""0.000000"" y=""0.000000"" z=""0.000000"" />
      <PovRearPassengerCameraOffset x=""0.000000"" y=""0.000000"" z=""0.000000"" />
      <vfxInfoName>YOURMODSNAME_YOURMODELNAME</vfxInfoName>
      <shouldUseCinematicViewMode value=""true"" />
      <shouldCameraTransitionOnClimbUpDown value=""false"" />
      <shouldCameraIgnoreExiting value=""false"" />
      <AllowPretendOccupants value=""true"" />
      <AllowJoyriding value=""true"" />
      <AllowSundayDriving value=""true"" />
      <AllowBodyColorMapping value=""true"" />
      <wheelScale value=""0.500000"" />
      <wheelScaleRear value=""0.500000"" />
      <dirtLevelMin value=""0.000000"" />
      <dirtLevelMax value=""0.450000"" />
      <envEffScaleMin value=""0.000000"" />
      <envEffScaleMax value=""1.000000"" />
      <envEffScaleMin2 value=""0.000000"" />
      <envEffScaleMax2 value=""1.000000"" />
      <damageMapScale value=""1.000000"" />
      <damageOffsetScale value=""1.000000"" />
      <diffuseTint value=""0x00FFFFFF"" />
      <steerWheelMult value=""0.700000"" />
      <HDTextureDist value=""5.000000"" />
      <lodDistances content=""float_array"">
        15.000000
        30.000000
        60.000000
        120.000000
        500.000000
        500.000000
      </lodDistances>
      <minSeatHeight value=""0.000000"" />
      <identicalModelSpawnDistance value=""20.000000"" />
      <maxNumOfSameColor value=""1"" />
      <defaultBodyHealth value=""1000.000000"" />
      <pretendOccupantsScale value=""1.000000"" />
      <visibleSpawnDistScale value=""1.000000"" />
      <trackerPathWidth value=""2.000000"" />
      <wheelReplacementCost value=""5000"" />
      <shouldReportCrimeAndTickOffPlayer value=""true"" />
      <shouldActivateHornOnDangerWhenPlayerDrives value=""false"" />
      <vehicleClass>VCS_SPORT</vehicleClass>
      <flags>FLAG_NONE</flags>
      <type>VEHICLE_TYPE_CAR</type>
      <plateType>VPT_FRONT_AND_BACK_PLATES</plateType>
      <dashboardType>VDT_DUKES</dashboardType>
      <vehicleWheelType>VWT_SPORT</vehicleWheelType>
      <wheelReplacementCostModifier value=""1.000000"" />
      <shouldForceAttachOppositeDirTrailer value=""true"" />
      <LightSettings value=""0"" />
      <vehicleLightSettings value=""0"" />
      <trailerSpawnSeed value=""3265724397"" />
      <swankness>SWANKNESS_4</swankness>
      <requiredExtras />
      <rewards />
      <cinematicPartCamera>
        <Item>YOURMODSNAME_YOURMODELNAME</Item>
      </cinematicPartCamera>
      <NmBraceOverrideSet />
      <buoyancySphereOffset x=""0.000000"" y=""0.000000"" z=""0.000000"" />
      <buoyancySphereSizeScale value=""1.000000"" />
      <pOverrideRagdollThreshold type=""NULL"" />
      <firstPersonDrivebyData>
        <Item>STD_YOURMODSNAME_YOURMODELNAME</Item>
      </firstPersonDrivebyData>
    </Item>
  </InitDatas>
  <txdRelationships>
    <Item>
      <parent>vehshare</parent>
      <child>{modelName}</child>
    </Item>
  </txdRelationships>
</CVehicleModelInfo__InitDataList>";
    }

    private static string GenerateHandlingMeta(string handlingId)
    {
        return $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<CHandlingDataMgr>
  <HandlingData>
    <Item type=""CHandlingData"">
      <handlingName>{handlingId}</handlingName>
      <fMass value=""1800.000000"" />
      <fInitialDragCoeff value=""8.500000"" />
      <fPercentSubmerged value=""85.000000"" />
      <vecCentreOfMassOffset x=""0.000000"" y=""0.000000"" z=""0.000000"" />
      <vecInertiaMultiplier x=""1.000000"" y=""1.000000"" z=""1.400000"" />
      <fDriveBiasFront value=""0.000000"" />
      <nInitialDriveGears value=""6"" />
      <fInitialDriveForce value=""0.350000"" />
      <fDriveInertia value=""1.000000"" />
      <fClutchChangeRateScaleUpShift value=""2.500000"" />
      <fClutchChangeRateScaleDownShift value=""2.500000"" />
      <fInitialDriveMaxFlatVel value=""160.000000"" />
      <fBrakeForce value=""1.000000"" />
      <fBrakeBiasFront value=""0.550000"" />
      <fHandBrakeForce value=""0.500000"" />
      <fSteeringLock value=""40.000000"" />
      <fTractionCurveMax value=""2.500000"" />
      <fTractionCurveMin value=""2.200000"" />
      <fTractionCurveLateral value=""22.500000"" />
      <fTractionSpringDeltaMax value=""0.150000"" />
      <fLowSpeedTractionLossMult value=""1.000000"" />
      <fCamberStiffnesss value=""0.000000"" />
      <fTractionBiasFront value=""0.485000"" />
      <fTractionLossMult value=""1.000000"" />
      <fSuspensionForce value=""2.000000"" />
      <fSuspensionCompDamp value=""1.200000"" />
      <fSuspensionReboundDamp value=""2.400000"" />
      <fSuspensionUpperLimit value=""0.100000"" />
      <fSuspensionLowerLimit value=""-0.130000"" />
      <fSuspensionRaise value=""0.000000"" />
      <fSuspensionBiasFront value=""0.500000"" />
      <fAntiRollBarForce value=""0.000000"" />
      <fAntiRollBarBiasFront value=""0.500000"" />
      <fRollCentreHeightFront value=""0.360000"" />
      <fRollCentreHeightRear value=""0.360000"" />
      <fCollisionDamageMult value=""1.000000"" />
      <fWeaponDamageMult value=""1.000000"" />
      <fDeformationDamageMult value=""0.800000"" />
      <fEngineDamageMult value=""1.500000"" />
      <fPetrolTankVolume value=""65.000000"" />
      <fOilVolume value=""5.000000"" />
      <fSeatOffsetDistX value=""0.000000"" />
      <fSeatOffsetDistY value=""0.000000"" />
      <fSeatOffsetDistZ value=""0.000000"" />
      <nMonetaryValue value=""35000"" />
      <strModelFlags>440010</strModelFlags>
      <strHandlingFlags>0</strHandlingFlags>
      <strDamageFlags>0</strDamageFlags>
      <AIHandling>AVERAGE</AIHandling>
      <SubHandlingData>
        <Item type=""CCarHandlingData"">
          <fBackEndPopUpCarImpulseMult value=""0.100000"" />
          <fBackEndPopUpBuildingImpulseMult value=""0.030000"" />
          <fBackEndPopUpMaxDeltaSpeed value=""0.600000"" />
        </Item>
        <Item type=""NULL"" />
        <Item type=""NULL"" />
      </SubHandlingData>
    </Item>
  </HandlingData>
</CHandlingDataMgr>";
    }

    private string GenerateBaseName(Library library, string category)
    {
        if (!library.Settings.Prefixes.TryGetValue(category, out var prefixSettings))
        {
            // Neue Kategorie anlegen
            prefixSettings = new PrefixSettings
            {
                Prefix = category.Length >= 3 ? category[..3] : category,
                NextNumber = 1
            };
            library.Settings.Prefixes[category] = prefixSettings;
        }

        var number = prefixSettings.NextNumber;
        return $"{prefixSettings.Prefix}_{category[0]}{number:D2}";
    }

    private static List<string> FindAllMetaFiles(string resourcePath)
    {
        var files = new List<string>();

        try
        {
            files.AddRange(Directory.GetFiles(resourcePath, "*.meta", SearchOption.AllDirectories));
        }
        catch (UnauthorizedAccessException)
        {
            // Zugriffsfehler ignorieren
        }

        return files;
    }

    private static async Task<ClassifiedMetaFiles> ClassifyMetaFilesAsync(List<string> metaFiles)
    {
        var result = new ClassifiedMetaFiles();

        foreach (var filePath in metaFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(filePath);

                // Versuche als XML zu parsen
                XDocument? doc = null;
                try
                {
                    doc = XDocument.Parse(content);
                }
                catch
                {
                    continue; // Kein gültiges XML
                }

                var root = doc.Root?.Name.LocalName;

                // Klassifizierung nach Root-Element und Inhalt
                if (root == "CVehicleModelInfo__InitDataList" ||
                    doc.Descendants("modelName").Any(e => e.Parent?.Element("handlingId") != null))
                {
                    // vehicles.meta (enthält modelName + handlingId)
                    result.VehiclesMeta.Add(filePath);
                }
                else if (root == "CHandlingDataMgr" ||
                         doc.Descendants("handlingName").Any() ||
                         doc.Descendants("Item").Any(e => e.Attribute("type")?.Value == "CHandlingData"))
                {
                    // handling.meta (enthält handlingName oder CHandlingData)
                    result.HandlingMeta.Add(filePath);
                }
                else if (root == "CVehicleModelInfoVariation" ||
                         doc.Descendants("sirenSettings").Any())
                {
                    // carvariations.meta (enthält sirenSettings)
                    result.CarVariationsMeta.Add(filePath);
                }
                else if (root == "CVehicleModelInfoVarGlobal" ||
                         doc.Descendants("Sirens").Any() ||
                         doc.Descendants("lightSettings").Any())
                {
                    // carcols.meta (enthält Sirens/lightSettings)
                    result.CarColsMeta.Add(filePath);
                }
            }
            catch
            {
                // Datei konnte nicht gelesen werden, überspringen
            }
        }

        return result;
    }

    private class ClassifiedMetaFiles
    {
        public List<string> VehiclesMeta { get; } = [];
        public List<string> HandlingMeta { get; } = [];
        public List<string> CarVariationsMeta { get; } = [];
        public List<string> CarColsMeta { get; } = [];
    }

    private static Dictionary<string, string> FindStreamFiles(string resourcePath)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Unterstützte Stream-Datei-Endungen
        var streamExtensions = new HashSet<string> { ".yft", ".ytd", ".rel", ".awc" };

        // Rekursiv im gesamten Verzeichnis suchen
        try
        {
            foreach (var file in Directory.GetFiles(resourcePath, "*.*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (streamExtensions.Contains(extension))
                {
                    var fileName = Path.GetFileName(file).ToLowerInvariant();
                    // Erste gefundene Datei gewinnt
                    if (!files.ContainsKey(fileName))
                    {
                        files[fileName] = file;
                    }
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Zugriffsfehler ignorieren
        }

        return files;
    }

    private static async Task<List<VehicleMetaEntry>> ParseVehiclesMetaAsync(string path)
    {
        var vehicles = new List<VehicleMetaEntry>();

        try
        {
            var content = await File.ReadAllTextAsync(path);
            var doc = XDocument.Parse(content);

            var items = doc.Descendants("Item")
                .Where(x => x.Element("modelName") != null);

            foreach (var item in items)
            {
                var modelName = item.Element("modelName")?.Value;
                if (string.IsNullOrEmpty(modelName))
                    continue;

                vehicles.Add(new VehicleMetaEntry
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

    private static async Task<List<string>> ParseHandlingMetaAsync(string path)
    {
        var handlingIds = new List<string>();

        try
        {
            var content = await File.ReadAllTextAsync(path);
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

    private static async Task<Dictionary<string, int>> ParseCarVariationsMetaAsync(string path)
    {
        var sirenSettings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var content = await File.ReadAllTextAsync(path);
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

    private class VehicleMetaEntry
    {
        public required string ModelName { get; set; }
        public string? TxdName { get; set; }
        public string? HandlingId { get; set; }
        public string? VehicleClass { get; set; }
    }
}
