using FiveMVehicleBuilder.Core.Models;

namespace FiveMVehicleBuilder.Core.Services;

public class CheckService : ICheckService
{
    private readonly IXmlValidationService _xmlValidationService;

    public CheckService(IXmlValidationService xmlValidationService)
    {
        _xmlValidationService = xmlValidationService;
    }

    public Task<CheckResult> CheckRecordAsync(Library library, string recordId)
    {
        var result = new CheckResult();

        if (!library.Records.TryGetValue(recordId, out var record))
        {
            result.Findings.Add(new CheckFinding
            {
                Severity = CheckSeverity.Error,
                Code = "RECORD_NOT_FOUND",
                Message = $"Record '{recordId}' nicht gefunden",
                RecordId = recordId
            });
            return Task.FromResult(result);
        }

        CheckSingleRecord(library, record, result);

        return Task.FromResult(result);
    }

    public Task<CheckResult> CheckAllAsync(Library library)
    {
        var result = new CheckResult();

        // Jeden Record einzeln prüfen
        foreach (var record in library.Records.Values)
        {
            CheckSingleRecord(library, record, result);
        }

        // Cross-Record-Prüfungen
        CheckDuplicateSpawnNames(library, result);
        CheckRecordPackAssignment(library, result);

        // Aktive Packs prüfen (inkl. SirenID-Duplikate)
        CheckActivePacksInternal(library, result);

        return Task.FromResult(result);
    }

    public Task<CheckResult> CheckActivePacksAsync(Library library)
    {
        var result = new CheckResult();
        CheckActivePacksInternal(library, result);
        return Task.FromResult(result);
    }

    private void CheckSingleRecord(Library library, Record record, CheckResult result)
    {
        var recordPath = Path.Combine(library.RecordsPath, record.Id, "original");
        var streamPath = Path.Combine(recordPath, "stream");
        var dataPath = Path.Combine(recordPath, "data");

        // Stream-Dateien prüfen
        CheckStreamFiles(record, streamPath, result);

        // Varianten prüfen
        foreach (var variant in record.Variants)
        {
            CheckVariant(library, record, variant, result);
        }

        // Handling-ID Konsistenz prüfen
        CheckHandlingId(record, dataPath, result);

        // Dateigröße prüfen
        CheckFileSizes(library, record, streamPath, result);
    }

    private static void CheckStreamFiles(Record record, string streamPath, CheckResult result)
    {
        if (!Directory.Exists(streamPath))
        {
            result.Findings.Add(new CheckFinding
            {
                Severity = CheckSeverity.Error,
                Code = "STREAM_DIR_MISSING",
                Message = $"Stream-Verzeichnis nicht gefunden",
                RecordId = record.Id,
                FilePath = streamPath
            });
            return;
        }

        // .yft prüfen
        var yftPath = Path.Combine(streamPath, $"{record.OriginalName}.yft");
        if (!File.Exists(yftPath))
        {
            result.Findings.Add(new CheckFinding
            {
                Severity = CheckSeverity.Error,
                Code = "YFT_MISSING",
                Message = $".yft-Datei fehlt für '{record.OriginalName}'",
                RecordId = record.Id,
                FilePath = yftPath
            });
        }

        // _hi.yft prüfen (optional, aber Warnung wenn fehlend)
        var hiYftPath = Path.Combine(streamPath, $"{record.OriginalName}_hi.yft");
        if (!File.Exists(hiYftPath))
        {
            result.Findings.Add(new CheckFinding
            {
                Severity = CheckSeverity.Warning,
                Code = "HI_YFT_MISSING",
                Message = $"_hi.yft-Datei fehlt für '{record.OriginalName}'",
                RecordId = record.Id,
                FilePath = hiYftPath
            });
        }

        // .ytd prüfen (optional, aber Warnung wenn fehlend)
        var ytdPath = Path.Combine(streamPath, $"{record.OriginalName}.ytd");
        if (!File.Exists(ytdPath))
        {
            result.Findings.Add(new CheckFinding
            {
                Severity = CheckSeverity.Warning,
                Code = "YTD_MISSING",
                Message = $".ytd-Datei fehlt für '{record.OriginalName}'",
                RecordId = record.Id,
                FilePath = ytdPath
            });
        }
    }

    private static void CheckVariant(Library library, Record record, Variant variant, CheckResult result)
    {
        // Spawnname-Länge prüfen
        var maxLength = library.Settings.Thresholds.MaxSpawnNameLength;
        if (variant.Name.Length > maxLength)
        {
            result.Findings.Add(new CheckFinding
            {
                Severity = CheckSeverity.Warning,
                Code = "SPAWN_NAME_TOO_LONG",
                Message = $"Spawnname '{variant.Name}' ist länger als {maxLength} Zeichen ({variant.Name.Length})",
                RecordId = record.Id,
                VariantName = variant.Name
            });
        }

        // Siren-Setting-ID prüfen (wenn vorhanden, muss > 0 sein)
        if (variant.SirenSettingId.HasValue && variant.SirenSettingId.Value <= 0)
        {
            result.Findings.Add(new CheckFinding
            {
                Severity = CheckSeverity.Warning,
                Code = "INVALID_SIREN_ID",
                Message = $"Ungültige Siren-Setting-ID: {variant.SirenSettingId.Value}",
                RecordId = record.Id,
                VariantName = variant.Name
            });
        }
    }

    private static void CheckHandlingId(Record record, string dataPath, CheckResult result)
    {
        var handlingMetaPath = Path.Combine(dataPath, "handling.meta");
        if (!File.Exists(handlingMetaPath))
        {
            // Keine handling.meta - wird vielleicht generiert
            return;
        }

        try
        {
            var content = File.ReadAllText(handlingMetaPath);
            if (!content.Contains(record.HandlingId, StringComparison.OrdinalIgnoreCase))
            {
                result.Findings.Add(new CheckFinding
                {
                    Severity = CheckSeverity.Warning,
                    Code = "HANDLING_ID_NOT_FOUND",
                    Message = $"HandlingId '{record.HandlingId}' nicht in handling.meta gefunden",
                    RecordId = record.Id,
                    FilePath = handlingMetaPath
                });
            }
        }
        catch
        {
            // Fehler beim Lesen ignorieren
        }
    }

    private static void CheckFileSizes(Library library, Record record, string streamPath, CheckResult result)
    {
        if (!Directory.Exists(streamPath))
            return;

        var maxSize = library.Settings.Thresholds.MaxFileSizeBytes;

        foreach (var file in Directory.GetFiles(streamPath))
        {
            var fileInfo = new FileInfo(file);
            if (fileInfo.Length > maxSize)
            {
                var sizeMb = fileInfo.Length / (1024.0 * 1024.0);
                var maxSizeMb = maxSize / (1024.0 * 1024.0);

                result.Findings.Add(new CheckFinding
                {
                    Severity = CheckSeverity.Warning,
                    Code = "FILE_TOO_LARGE",
                    Message = $"Datei '{fileInfo.Name}' ist {sizeMb:F1} MB groß (max. {maxSizeMb:F0} MB)",
                    RecordId = record.Id,
                    FilePath = file
                });
            }
        }
    }

    private static void CheckDuplicateSpawnNames(Library library, CheckResult result)
    {
        var spawnNames = new Dictionary<string, (string RecordId, string VariantName)>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in library.Records.Values)
        {
            foreach (var variant in record.Variants)
            {
                if (spawnNames.TryGetValue(variant.Name, out var existing))
                {
                    result.Findings.Add(new CheckFinding
                    {
                        Severity = CheckSeverity.Error,
                        Code = "DUPLICATE_SPAWN_NAME",
                        Message = $"Doppelter Spawnname '{variant.Name}' (auch in Record '{existing.RecordId}')",
                        RecordId = record.Id,
                        VariantName = variant.Name
                    });
                }
                else
                {
                    spawnNames[variant.Name] = (record.Id, variant.Name);
                }
            }
        }
    }

    private static void CheckRecordPackAssignment(Library library, CheckResult result)
    {
        var recordPacks = new Dictionary<string, List<string>>();

        foreach (var pack in library.Packs.Values)
        {
            foreach (var recordId in pack.Records)
            {
                if (!recordPacks.ContainsKey(recordId))
                {
                    recordPacks[recordId] = new List<string>();
                }
                recordPacks[recordId].Add(pack.Name);
            }
        }

        foreach (var (recordId, packs) in recordPacks)
        {
            if (packs.Count > 1)
            {
                result.Findings.Add(new CheckFinding
                {
                    Severity = CheckSeverity.Error,
                    Code = "RECORD_IN_MULTIPLE_PACKS",
                    Message = $"Record '{recordId}' ist in mehreren Packs: {string.Join(", ", packs)}",
                    RecordId = recordId
                });
            }
        }
    }

    private void CheckActivePacksInternal(Library library, CheckResult result)
    {
        var activePacks = library.Packs.Values.Where(p => p.Active).ToList();
        if (activePacks.Count == 0)
            return;

        // Alle Varianten aus aktiven Packs sammeln
        var allVariants = new List<(string RecordId, Variant Variant)>();
        foreach (var pack in activePacks)
        {
            foreach (var recordId in pack.Records)
            {
                if (library.Records.TryGetValue(recordId, out var record))
                {
                    foreach (var variant in record.Variants)
                    {
                        allVariants.Add((recordId, variant));
                    }
                }
            }
        }

        // Doppelte Siren-Setting-IDs prüfen
        CheckDuplicateSirenIds(allVariants, result);
    }

    private static void CheckDuplicateSirenIds(
        List<(string RecordId, Variant Variant)> variants,
        CheckResult result,
        string code = "DUPLICATE_SIREN_ID")
    {
        var sirenIds = new Dictionary<int, (string RecordId, string VariantName)>();

        foreach (var (recordId, variant) in variants)
        {
            if (!variant.SirenSettingId.HasValue)
                continue;

            var sirenId = variant.SirenSettingId.Value;
            if (sirenIds.TryGetValue(sirenId, out var existing))
            {
                result.Findings.Add(new CheckFinding
                {
                    Severity = CheckSeverity.Error,
                    Code = code,
                    Message = $"Doppelte Siren-Setting-ID {sirenId} (auch in '{existing.RecordId}/{existing.VariantName}')",
                    RecordId = recordId,
                    VariantName = variant.Name
                });
            }
            else
            {
                sirenIds[sirenId] = (recordId, variant.Name);
            }
        }
    }

    public Task<CheckResult> CheckAllSirenIdsAsync(Library library)
    {
        var result = new CheckResult();
        CheckAllSirenIdsInternal(library, result);
        return Task.FromResult(result);
    }

    private static void CheckAllSirenIdsInternal(Library library, CheckResult result)
    {
        // Alle Varianten aus allen Records sammeln (nicht nur aktive Packs)
        var allVariants = new List<(string RecordId, Variant Variant)>();

        foreach (var record in library.Records.Values)
        {
            foreach (var variant in record.Variants)
            {
                allVariants.Add((record.Id, variant));
            }
        }

        // Doppelte SirenIDs prüfen (global)
        CheckDuplicateSirenIds(allVariants, result, "DUPLICATE_SIREN_ID_GLOBAL");
    }

    public async Task<CheckResult> CheckMetaFileValidityAsync(Library library, string recordId)
    {
        var result = new CheckResult();

        if (!library.Records.TryGetValue(recordId, out var record))
        {
            result.Findings.Add(new CheckFinding
            {
                Severity = CheckSeverity.Error,
                Code = "RECORD_NOT_FOUND",
                Message = $"Record '{recordId}' nicht gefunden",
                RecordId = recordId
            });
            return result;
        }

        var dataPath = Path.Combine(library.RecordsPath, record.Id, "original", "data");

        if (!Directory.Exists(dataPath))
            return result;

        var metaFiles = new[] { "vehicles.meta", "handling.meta", "carvariations.meta", "carcols.meta" };

        foreach (var metaFile in metaFiles)
        {
            var filePath = Path.Combine(dataPath, metaFile);
            if (!File.Exists(filePath))
                continue;

            var validation = await _xmlValidationService.ValidateXmlFileAsync(filePath);
            if (!validation.IsValid)
            {
                result.Findings.Add(new CheckFinding
                {
                    Severity = CheckSeverity.Error,
                    Code = "INVALID_XML",
                    Message = validation.FormattedError,
                    RecordId = recordId,
                    FilePath = filePath
                });
            }
        }

        return result;
    }

    public Task<int> FixDuplicateSirenIdsAsync(Library library)
    {
        // Alle SirenIDs sammeln und Duplikate finden
        var sirenIdUsage = new Dictionary<int, List<(string RecordId, Variant Variant)>>();

        foreach (var record in library.Records.Values)
        {
            foreach (var variant in record.Variants)
            {
                if (!variant.SirenSettingId.HasValue)
                    continue;

                var sirenId = variant.SirenSettingId.Value;
                if (!sirenIdUsage.ContainsKey(sirenId))
                {
                    sirenIdUsage[sirenId] = [];
                }
                sirenIdUsage[sirenId].Add((record.Id, variant));
            }
        }

        var fixedCount = 0;

        // Duplikate fixen (ab dem zweiten Vorkommen)
        foreach (var (sirenId, usages) in sirenIdUsage)
        {
            if (usages.Count <= 1)
                continue;

            // Erstes Vorkommen behalten, alle weiteren neu vergeben
            for (var i = 1; i < usages.Count; i++)
            {
                var (_, variant) = usages[i];
                variant.SirenSettingId = library.Settings.IdRanges.NextSirenSettingId;
                library.Settings.IdRanges.NextSirenSettingId++;
                fixedCount++;
            }
        }

        return Task.FromResult(fixedCount);
    }
}
