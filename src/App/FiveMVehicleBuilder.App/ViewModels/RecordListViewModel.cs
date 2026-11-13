using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FiveMVehicleBuilder.App.Services;
using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.App.ViewModels;

public partial class RecordListViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;
    private readonly MainViewModel _mainViewModel;
    private readonly ImportService _importService;
    private readonly RpfService _rpfService;

    private Library? _library;
    private List<RecordViewModel> _allRecords = [];

    [ObservableProperty]
    private ObservableCollection<RecordViewModel> _records = [];

    [ObservableProperty]
    private RecordViewModel? _selectedRecord;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FilteredCount))]
    private string? _searchText;

    [ObservableProperty]
    private string? _selectedCategory;

    [ObservableProperty]
    private ObservableCollection<string> _categories = [];

    public int FilteredCount => Records.Count;
    public int TotalCount => _allRecords.Count;

    public RecordListViewModel(IDialogService dialogService, MainViewModel mainViewModel)
    {
        _dialogService = dialogService;
        _mainViewModel = mainViewModel;
        _importService = new ImportService();
        _rpfService = new RpfService();
    }

    public Task LoadRecordsAsync(Library library)
    {
        _library = library;

        _allRecords = library.Records.Values
            .Select(r => new RecordViewModel(r, library))
            .OrderBy(r => r.Category)
            .ThenBy(r => r.Id)
            .ToList();

        // Kategorien extrahieren
        var cats = _allRecords
            .Select(r => r.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        Categories.Clear();
        Categories.Add("Alle");
        foreach (var cat in cats)
        {
            Categories.Add(cat);
        }
        SelectedCategory = "Alle";

        ApplyFilter();
        return Task.CompletedTask;
    }

    partial void OnSearchTextChanged(string? value)
    {
        ApplyFilter();
    }

    partial void OnSelectedCategoryChanged(string? value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = _allRecords.AsEnumerable();

        // Kategorie-Filter
        if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "Alle")
        {
            filtered = filtered.Where(r => r.Category == SelectedCategory);
        }

        // Text-Filter
        if (!string.IsNullOrEmpty(SearchText))
        {
            var search = SearchText.ToLowerInvariant();
            filtered = filtered.Where(r =>
                r.Id.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.OriginalName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                r.Variants.Any(v => v.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        Records = new ObservableCollection<RecordViewModel>(filtered);
        OnPropertyChanged(nameof(FilteredCount));
    }

    private static readonly string[] DefaultCategories =
    [
        "einsatz",
        "sport",
        "suv",
        "limousine",
        "coupe",
        "muscle",
        "classic",
        "offroad",
        "motorrad",
        "lkw",
        "van",
        "boot",
        "helikopter",
        "flugzeug",
        "sonstige"
    ];

    [RelayCommand]
    private async Task ImportAsync()
    {
        var path = _dialogService.SelectFolder("FiveM-Resource zum Importieren auswählen");
        if (string.IsNullOrEmpty(path) || _library == null)
            return;

        IsBusy = true;
        BusyMessage = "Analysiere Resource...";

        try
        {
            var analysis = await _importService.AnalyzeAsync(path);

            // Warnungen anzeigen
            if (analysis.Warnings.Count > 0)
            {
                var warningText = string.Join("\n", analysis.Warnings);
                _dialogService.ShowInfo($"Hinweise:\n\n{warningText}");
            }

            if (analysis.Vehicles.Count == 0)
            {
                _dialogService.ShowError("Keine importierbaren Fahrzeuge gefunden.");
                return;
            }

            IsBusy = false;

            // Verfügbare Kategorien (bestehende + Standard)
            var existingCategories = _allRecords
                .Select(r => r.Category)
                .Distinct()
                .ToList();

            var allCategories = existingCategories
                .Union(DefaultCategories)
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            var category = _dialogService.ShowSelection(
                $"Kategorie für {analysis.Vehicles.Count} Fahrzeug(e):",
                allCategories,
                "Kategorie auswählen");

            if (string.IsNullOrEmpty(category))
                return;

            // Für jedes Fahrzeug Namen abfragen
            var imports = new List<(AnalyzedVehicle vehicle, string name)>();
            foreach (var vehicle in analysis.Vehicles)
            {
                var suggestedName = $"ev_{vehicle.ModelName.ToLowerInvariant()}";
                var metaHint = vehicle.NeedsMetaGeneration ? "\n(Meta-Dateien werden generiert)" : "";
                var name = _dialogService.ShowInput(
                    $"Name für '{vehicle.ModelName}':{metaHint}",
                    "Record-Name",
                    suggestedName);

                if (string.IsNullOrWhiteSpace(name))
                    continue; // Übersprungen

                // Name bereinigen
                name = System.Text.RegularExpressions.Regex.Replace(
                    name.ToLowerInvariant().Trim(),
                    @"[^a-z0-9_]", "_");

                // Prüfen ob Name bereits existiert
                if (_library.Records.ContainsKey(name))
                {
                    _dialogService.ShowError($"Record '{name}' existiert bereits.");
                    continue;
                }

                imports.Add((vehicle, name));
            }

            if (imports.Count == 0)
            {
                _dialogService.ShowInfo("Keine Fahrzeuge importiert.");
                return;
            }

            IsBusy = true;
            BusyMessage = "Importiere Fahrzeuge...";

            foreach (var (vehicle, name) in imports)
            {
                await _importService.ImportAsync(_library, vehicle, category, customName: name);
            }

            // Library speichern und neu laden
            await _mainViewModel.SaveLibraryCommand.ExecuteAsync(null);
            await LoadRecordsAsync(_library);

            _dialogService.ShowInfo($"{analysis.Vehicles.Count} Fahrzeug(e) importiert.");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Import fehlgeschlagen: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task ImportFromRpfAsync()
    {
        if (_library == null)
            return;

        // RPF-Browser Dialog öffnen
        var selectedVehicles = _dialogService.ShowRpfBrowser(_rpfService);
        if (selectedVehicles == null || selectedVehicles.Count == 0)
            return;

        // Verfügbare Kategorien (bestehende + Standard)
        var existingCategories = _allRecords
            .Select(r => r.Category)
            .Distinct()
            .ToList();

        var allCategories = existingCategories
            .Union(DefaultCategories)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        // Kategorie-Auswahl (einmal für alle)
        var category = _dialogService.ShowSelection(
            $"Kategorie für {selectedVehicles.Count} Fahrzeug(e):",
            allCategories,
            "Kategorie auswählen");

        if (string.IsNullOrEmpty(category))
            return;

        IsBusy = true;
        var importedCount = 0;

        try
        {
            // Für jedes Fahrzeug: Name-Dialog + Import
            foreach (var vehicle in selectedVehicles)
            {
                BusyMessage = $"Verarbeite {vehicle.ModelName}...";

                var suggestedName = $"ev_{vehicle.ModelName.ToLowerInvariant()}";
                var name = _dialogService.ShowInput(
                    $"Name für '{vehicle.ModelName}':",
                    "Record-Name",
                    suggestedName);

                if (string.IsNullOrWhiteSpace(name))
                    continue; // Übersprungen

                // Name bereinigen
                name = System.Text.RegularExpressions.Regex.Replace(
                    name.ToLowerInvariant().Trim(),
                    @"[^a-z0-9_]", "_");

                // Prüfen ob Name bereits existiert
                if (_library.Records.ContainsKey(name))
                {
                    _dialogService.ShowError($"Record '{name}' existiert bereits.");
                    continue;
                }

                // Temporär extrahieren
                BusyMessage = $"Extrahiere {vehicle.ModelName}...";
                var tempPath = await _rpfService.ExtractToTempAsync(vehicle);

                // Normalen Import-Flow nutzen
                var analysis = await _importService.AnalyzeAsync(tempPath);
                if (analysis.Vehicles.Count > 0)
                {
                    await _importService.ImportAsync(_library, analysis.Vehicles[0], category, customName: name);
                    importedCount++;
                }
            }

            // Temp aufräumen
            _rpfService.CleanupTemp();

            // Library speichern und UI aktualisieren
            await _mainViewModel.SaveLibraryCommand.ExecuteAsync(null);
            await LoadRecordsAsync(_library);

            _dialogService.ShowInfo($"{importedCount} Fahrzeug(e) aus RPF importiert.");
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"RPF-Import fehlgeschlagen: {ex.Message}");
        }
        finally
        {
            _rpfService.CleanupTemp();
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task EditRecordAsync()
    {
        if (SelectedRecord == null || _library == null)
            return;

        var oldId = SelectedRecord.Id;

        // Record aus Library holen (nicht das ViewModel)
        if (!_library.Records.TryGetValue(oldId, out var record))
            return;

        // Alle Kategorien sammeln
        var categories = _allRecords
            .Select(r => r.Category)
            .Union(DefaultCategories)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        var existingIds = _library.Records.Keys.ToList();

        // Funktion die bei jedem Aufruf die nächste freie SirenID zurückgibt und inkrementiert
        Func<int> getNextSirenId = () =>
        {
            var id = _library.Settings.IdRanges.NextSirenSettingId;
            _library.Settings.IdRanges.NextSirenSettingId++;
            return id;
        };

        var (saved, newId) = _dialogService.ShowRecordEdit(record, categories, existingIds, getNextSirenId);

        if (saved)
        {
            // Wurde umbenannt?
            if (!string.IsNullOrEmpty(newId) && newId != oldId)
            {
                await RenameRecordAsync(record, oldId, newId);
            }

            await _mainViewModel.SaveLibraryCommand.ExecuteAsync(null);
            await LoadRecordsAsync(_library);
        }
    }

    private async Task RenameRecordAsync(Record record, string oldId, string newId)
    {
        if (_library == null) return;

        // 1. Verzeichnis umbenennen
        var oldPath = Path.Combine(_library.RecordsPath, oldId);
        var newPath = Path.Combine(_library.RecordsPath, newId);

        if (Directory.Exists(oldPath))
        {
            Directory.Move(oldPath, newPath);
        }

        // 2. Record-ID aktualisieren
        record.Id = newId;

        // 3. Aus altem Key entfernen und mit neuem hinzufügen
        _library.Records.Remove(oldId);
        _library.Records[newId] = record;

        // 4. Pack-Referenzen aktualisieren
        foreach (var pack in _library.Packs.Values)
        {
            if (pack.Records.Remove(oldId))
            {
                pack.Records.Add(newId);
            }
        }

        // 5. Alte record.json löschen (wird mit neuem Namen neu erstellt)
        var oldRecordJson = Path.Combine(newPath, "record.json");
        if (File.Exists(oldRecordJson))
        {
            File.Delete(oldRecordJson);
        }

        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DeleteRecordAsync()
    {
        if (SelectedRecord == null || _library == null)
            return;

        if (!_dialogService.Confirm(
            $"Record '{SelectedRecord.Id}' wirklich löschen?\n\nDies kann nicht rückgängig gemacht werden.",
            "Record löschen"))
        {
            return;
        }

        // Aus Packs entfernen
        foreach (var pack in _library.Packs.Values)
        {
            pack.Records.Remove(SelectedRecord.Id);
        }

        // Record-Verzeichnis löschen
        var recordPath = Path.Combine(_library.RecordsPath, SelectedRecord.Id);
        if (Directory.Exists(recordPath))
        {
            Directory.Delete(recordPath, true);
        }

        // Aus Library entfernen
        _library.Records.Remove(SelectedRecord.Id);

        await _mainViewModel.SaveLibraryCommand.ExecuteAsync(null);
        await LoadRecordsAsync(_library);
    }

    [RelayCommand]
    private async Task AssignToPackAsync()
    {
        if (SelectedRecord == null || _library == null)
            return;

        var packs = _library.Packs.Values
            .Select(p => (p.Name, p.Records.Count))
            .OrderBy(p => p.Name)
            .ToList();

        if (packs.Count == 0)
        {
            _dialogService.ShowError("Keine Packs vorhanden. Erstellen Sie zuerst ein Pack.");
            return;
        }

        var result = _dialogService.ShowAssignToPack(
            SelectedRecord.Id,
            packs,
            SelectedRecord.PackName);

        if (result == null)
            return;

        var (packName, remove) = result.Value;

        // Erst aus allen Packs entfernen
        foreach (var pack in _library.Packs.Values)
        {
            pack.Records.Remove(SelectedRecord.Id);
        }

        // Dann zum neuen Pack hinzufügen (falls nicht Remove)
        if (!remove && !string.IsNullOrEmpty(packName))
        {
            if (_library.Packs.TryGetValue(packName, out var targetPack))
            {
                targetPack.Records.Add(SelectedRecord.Id);
            }
        }

        await _mainViewModel.SaveLibraryCommand.ExecuteAsync(null);
        await LoadRecordsAsync(_library);
    }
}

public partial class RecordViewModel : ObservableObject
{
    private readonly Record _record;
    private readonly Library _library;

    public string Id => _record.Id;
    public string OriginalName => _record.OriginalName;
    public string Category => _record.Category;
    public string HandlingId => _record.HandlingId;
    public DateOnly ImportedAt => _record.ImportedAt;
    public string? Source => _record.Source;

    public int VariantCount => _record.Variants.Count;
    public List<string> Variants => _record.Variants.Select(v => v.Name).ToList();
    public string VariantsDisplay => string.Join(", ", Variants);

    public string? PackName => _library.Packs.Values
        .FirstOrDefault(p => p.Records.Contains(Id))?.Name;

    public RecordViewModel(Record record, Library library)
    {
        _record = record;
        _library = library;
    }
}
