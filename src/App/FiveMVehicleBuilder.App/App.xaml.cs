using System.Windows;
using System.Windows.Media;
using FiveMVehicleBuilder.App.Services;
using FiveMVehicleBuilder.App.ViewModels;
using FiveMVehicleBuilder.App.Views;
using FiveMVehicleBuilder.Core;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Globaler Exception-Handler
        DispatcherUnhandledException += (s, ex) =>
        {
            var errorText = $"{ex.Exception.GetType().Name}:\n{ex.Exception.Message}\n\nStackTrace:\n{ex.Exception.StackTrace}";

            var result = MessageBox.Show(
                $"Unbehandelte Exception:\n\n{errorText}\n\n'Ja' klicken um in Zwischenablage zu kopieren.",
                "Fehler",
                MessageBoxButton.YesNo,
                MessageBoxImage.Error);

            if (result == MessageBoxResult.Yes)
            {
                Clipboard.SetText(errorText);
            }

            ex.Handled = true;
        };

        var settings = AppSettings.Load();
        var libraryService = new LibraryService();

        // Prüfen ob eine gültige Bibliothek konfiguriert ist
        if (string.IsNullOrEmpty(settings.LibraryPath) ||
            !libraryService.IsValidLibrary(settings.LibraryPath))
        {
            // Onboarding anzeigen
            if (!ShowOnboarding())
            {
                // Benutzer hat abgebrochen
                Shutdown();
                return;
            }
        }

        // Hauptfenster anzeigen
        ShowMainWindow();
    }

    private bool ShowOnboarding()
    {
        var dialogService = new DialogService();
        var viewModel = new OnboardingViewModel(dialogService);
        var libraryReady = false;

        var window = new Window
        {
            Title = "FiveM Vehicle Builder - Setup",
            Width = 600,
            Height = 450,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e)),
            Content = new OnboardingView { DataContext = viewModel }
        };

        // Wenn Bibliothek bereit ist, Fenster schließen
        viewModel.LibraryReady += () =>
        {
            libraryReady = true;
            window.DialogResult = true;
            window.Close();
        };

        window.ShowDialog();
        return libraryReady;
    }

    private void ShowMainWindow()
    {
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
    }
}
