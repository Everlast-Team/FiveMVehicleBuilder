using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FiveMVehicleBuilder.App.Services;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Models;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.App.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly IDialogService _dialogService;
    private readonly LibraryService _libraryService;

    [ObservableProperty]
    private Library? _library;

    [ObservableProperty]
    private string? _libraryPath;

    [ObservableProperty]
    private ViewModelBase? _currentView;

    [ObservableProperty]
    private string _currentViewName = "Records";

    // Child ViewModels
    public RecordListViewModel RecordListViewModel { get; }
    public PackListViewModel PackListViewModel { get; }

    public MainViewModel(
        INavigationService navigationService,
        IDialogService dialogService)
    {
        _navigationService = navigationService;
        _dialogService = dialogService;
        _libraryService = new LibraryService();

        RecordListViewModel = new RecordListViewModel(dialogService, this);
        PackListViewModel = new PackListViewModel(dialogService, this);

        _navigationService.NavigationRequested += OnNavigationRequested;
    }

    public override async Task InitializeAsync()
    {
        var settings = AppSettings.Load();
        LibraryPath = settings.LibraryPath;

        if (!string.IsNullOrEmpty(LibraryPath) && _libraryService.IsValidLibrary(LibraryPath))
        {
            await LoadLibraryAsync();
        }

        // Standard-View
        CurrentView = RecordListViewModel;
    }

    [RelayCommand]
    private async Task LoadLibraryAsync()
    {
        if (string.IsNullOrEmpty(LibraryPath))
            return;

        IsBusy = true;
        BusyMessage = "Lade Bibliothek...";

        try
        {
            Library = await _libraryService.OpenAsync(LibraryPath);
            await RecordListViewModel.LoadRecordsAsync(Library);
            await PackListViewModel.LoadPacksAsync(Library);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Fehler beim Laden: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private async Task SaveLibraryAsync()
    {
        if (Library == null)
            return;

        IsBusy = true;
        BusyMessage = "Speichere...";

        try
        {
            await _libraryService.SaveAsync(Library);
        }
        catch (Exception ex)
        {
            _dialogService.ShowError($"Fehler beim Speichern: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            BusyMessage = null;
        }
    }

    [RelayCommand]
    private void ShowRecords()
    {
        CurrentView = RecordListViewModel;
        CurrentViewName = "Records";
    }

    [RelayCommand]
    private void ShowPacks()
    {
        CurrentView = PackListViewModel;
        CurrentViewName = "Packs";
    }

    private void OnNavigationRequested(Type viewModelType)
    {
        if (viewModelType == typeof(RecordListViewModel))
        {
            ShowRecords();
        }
        else if (viewModelType == typeof(PackListViewModel))
        {
            ShowPacks();
        }
    }
}
