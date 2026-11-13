using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FiveMVehicleBuilder.App.Services;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.App.ViewModels;

public partial class OnboardingViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;
    private readonly LibraryService _libraryService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateLibraryCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenLibraryCommand))]
    private string? _libraryPath;

    [ObservableProperty]
    private bool _isNewLibrary = true;

    [ObservableProperty]
    private bool _initGit = true;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _isSuccess;

    public event Action? LibraryReady;

    public OnboardingViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
        _libraryService = new LibraryService();
    }

    [RelayCommand]
    private void SelectFolder()
    {
        var path = _dialogService.SelectFolder("Bibliothek-Ordner auswählen");
        if (!string.IsNullOrEmpty(path))
        {
            LibraryPath = path;

            // Prüfen ob bereits eine Bibliothek existiert
            IsNewLibrary = !_libraryService.IsValidLibrary(path);
        }
    }

    [RelayCommand(CanExecute = nameof(CanCreateLibrary))]
    private async Task CreateLibraryAsync()
    {
        if (string.IsNullOrEmpty(LibraryPath))
            return;

        IsBusy = true;
        BusyMessage = "Erstelle Bibliothek...";
        StatusMessage = null;

        try
        {
            await _libraryService.CreateAsync(LibraryPath, InitGit);

            // Pfad speichern
            var settings = AppSettings.Load();
            settings.LibraryPath = LibraryPath;
            settings.Save();

            StatusMessage = "Bibliothek erfolgreich erstellt!";
            IsSuccess = true;

            // Kurz warten, dann zum Hauptfenster
            await Task.Delay(1000);
            LibraryReady?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    private bool CanCreateLibrary() => !string.IsNullOrEmpty(LibraryPath) && IsNewLibrary;

    [RelayCommand(CanExecute = nameof(CanOpenLibrary))]
    private async Task OpenLibraryAsync()
    {
        if (string.IsNullOrEmpty(LibraryPath))
            return;

        IsBusy = true;
        BusyMessage = "Öffne Bibliothek...";
        StatusMessage = null;

        try
        {
            await _libraryService.OpenAsync(LibraryPath);

            // Pfad speichern
            var settings = AppSettings.Load();
            settings.LibraryPath = LibraryPath;
            settings.Save();

            StatusMessage = "Bibliothek geöffnet!";
            IsSuccess = true;

            await Task.Delay(500);
            LibraryReady?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Fehler: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    private bool CanOpenLibrary() => !string.IsNullOrEmpty(LibraryPath) && !IsNewLibrary;
}
