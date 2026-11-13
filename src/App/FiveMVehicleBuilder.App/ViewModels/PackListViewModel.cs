using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FiveMVehicleBuilder.App.Services;
using FiveMVehicleBuilder.App.Views.Dialogs;
using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.App.ViewModels;

public partial class PackListViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;
    private readonly MainViewModel _mainViewModel;
    private readonly XmlValidationService _xmlValidationService;
    private readonly BuildService _buildService;
    private readonly CheckService _checkService;
    private readonly ExportService _exportService;

    private Library? _library;

    [ObservableProperty]
    private ObservableCollection<PackViewModel> _packs = [];

    [ObservableProperty]
    private PackViewModel? _selectedPack;

    [ObservableProperty]
    private double _buildProgress;

    [ObservableProperty]
    private string? _buildStatus;

    public PackListViewModel(IDialogService dialogService, MainViewModel mainViewModel)
    {
        _dialogService = dialogService;
        _mainViewModel = mainViewModel;
        _xmlValidationService = new XmlValidationService();
        _buildService = new BuildService(_xmlValidationService);
        _checkService = new CheckService(_xmlValidationService);
        _exportService = new ExportService();
    }

    public Task LoadPacksAsync(Library library)
    {
        _library = library;

        var packs = library.Packs.Values
            .Select(p => new PackViewModel(p, library))
            .OrderBy(p => p.Name)
            .ToList();

        Packs = new ObservableCollection<PackViewModel>(packs);

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CreatePackAsync()
    {
        if (_library == null)
            return;

        var name = _dialogService.ShowInput(
            "Name des neuen Packs:",
            "Neues Pack erstellen",
            "neues_pack");

        if (string.IsNullOrWhiteSpace(name))
            return;

        // Name bereinigen (nur lowercase, keine Sonderzeichen)
        name = System.Text.RegularExpressions.Regex.Replace(
            name.ToLowerInvariant().Trim(),
            @"[^a-z0-9_]", "_");

        if (_library.Packs.ContainsKey(name))
        {
            _dialogService.ShowError($"Pack '{name}' existiert bereits.");
            return;
        }

        var pack = new Pack
        {
            Name = name,
            ResourceName = $"vehicles_{name}",
            Active = true,
            Records = []
        };

        _library.Packs[name] = pack;

        await _mainViewModel.SaveLibraryCommand.ExecuteAsync(null);
        await LoadPacksAsync(_library);
    }

    [RelayCommand]
    private async Task DeletePackAsync()
    {
        if (SelectedPack == null || _library == null)
            return;

        if (!_dialogService.Confirm(
            $"Pack '{SelectedPack.Name}' wirklich löschen?\n\nDie Records bleiben erhalten.",
            "Pack löschen"))
        {
            return;
        }

        // Pack-JSON-Datei löschen
        var packPath = System.IO.Path.Combine(_library.PacksPath, $"{SelectedPack.Name}.json");
        if (System.IO.File.Exists(packPath))
        {
            System.IO.File.Delete(packPath);
        }

        // Build-Ordner löschen falls vorhanden
        var buildPath = System.IO.Path.Combine(_library.BuildPath, SelectedPack.ResourceName);
        if (System.IO.Directory.Exists(buildPath))
        {
            System.IO.Directory.Delete(buildPath, true);
        }

        _library.Packs.Remove(SelectedPack.Name);

        await _mainViewModel.SaveLibraryCommand.ExecuteAsync(null);
        await LoadPacksAsync(_library);
    }

    [RelayCommand]
    private async Task BuildPackAsync()
    {
        if (SelectedPack == null || _library == null)
            return;

        IsBusy = true;
        BuildProgress = 0;
        BuildStatus = "Prüfe Records...";

        try
        {
            // Erst prüfen
            var checkResult = await _checkService.CheckAllAsync(_library);
            if (checkResult.HasErrors)
            {
                _dialogService.ShowError(
                    $"Build nicht möglich: {checkResult.ErrorCount} Fehler gefunden.\n\n" +
                    string.Join("\n", checkResult.Findings
                        .Where(f => f.Severity == CheckSeverity.Error)
                        .Take(5)
                        .Select(f => $"• {f.Message}")));
                return;
            }

            BuildStatus = "Baue Resource...";

            var progress = new Progress<BuildProgress>(p =>
            {
                BuildProgress = p.Percentage;
                BuildStatus = $"{p.Stage}: {p.CurrentItem}";
            });

            var result = await _buildService.BuildPackAsync(_library, SelectedPack.Name, progress);

            if (result.Success)
            {
                _dialogService.ShowInfo(
                    $"Build erfolgreich!\n\n" +
                    $"Ausgabe: {result.OutputPath}\n" +
                    $"Records: {result.Statistics?.RecordCount}\n" +
                    $"Varianten: {result.Statistics?.VariantCount}\n" +
                    $"Größe: {result.Statistics?.TotalSizeBytes / 1024.0:F1} KB");
            }
            else
            {
                _dialogService.ShowError($"Build fehlgeschlagen: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Fehler: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            BuildProgress = 0;
            BuildStatus = null;
        }
    }

    [RelayCommand]
    private void ToggleActive()
    {
        if (SelectedPack == null || _library == null)
            return;

        if (_library.Packs.TryGetValue(SelectedPack.Name, out var pack))
        {
            pack.Active = !pack.Active;
            SelectedPack.IsActive = pack.Active;
        }
    }

    [RelayCommand]
    private async Task ExportPackAsync()
    {
        if (SelectedPack == null || _library == null)
            return;

        var dialog = new ExportDialog();
        if (dialog.ShowDialog() != true)
            return;

        IsBusy = true;
        BusyMessage = "Exportiere...";

        try
        {
            var result = await _exportService.ExportAsync(_library, SelectedPack.Name, dialog.SelectedFormat);

            if (result.Success)
            {
                _dialogService.ShowInfo(
                    $"Export erfolgreich!\n\n" +
                    $"Datei: {result.OutputPath}\n" +
                    $"Fahrzeuge: {result.VehicleCount}");
            }
            else
            {
                _dialogService.ShowError($"Export fehlgeschlagen: {result.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Fehler: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public partial class PackViewModel : ObservableObject
{
    private readonly Pack _pack;
    private readonly Library _library;

    public string Name => _pack.Name;
    public string ResourceName => _pack.ResourceName;

    [ObservableProperty]
    private bool _isActive;

    public int RecordCount => _pack.Records.Count;

    public int VariantCount => _pack.Records
        .Where(id => _library.Records.ContainsKey(id))
        .Sum(id => _library.Records[id].Variants.Count);

    public List<string> RecordIds => _pack.Records.ToList();

    public PackViewModel(Pack pack, Library library)
    {
        _pack = pack;
        _library = library;
        _isActive = pack.Active;
    }
}
