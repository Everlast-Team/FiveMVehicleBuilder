using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using FiveMVehicleBuilder.Core.Services;
using Microsoft.Win32;

namespace FiveMVehicleBuilder.App.Views.Dialogs;

public partial class RpfBrowserDialog : Window, INotifyPropertyChanged
{
    private readonly IRpfService _rpfService;
    private string _filePath = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FilePath
    {
        get => _filePath;
        set
        {
            _filePath = value;
            OnPropertyChanged(nameof(FilePath));
        }
    }

    public ObservableCollection<RpfVehicleViewModel> Vehicles { get; } = [];
    public ObservableCollection<string> Warnings { get; } = [];

    public List<RpfVehicleEntry>? SelectedVehicles { get; private set; }

    public bool HasSelection => Vehicles.Any(v => v.IsSelected);

    public string SelectionCountText
    {
        get
        {
            var count = Vehicles.Count(v => v.IsSelected);
            return count == 1 ? "1 Fahrzeug ausgewählt" : $"{count} Fahrzeuge ausgewählt";
        }
    }

    public RpfBrowserDialog(IRpfService rpfService)
    {
        _rpfService = rpfService;
        InitializeComponent();
        DataContext = this;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void UpdateSelectionState()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionCountText));
    }

    private async void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "RPF-Datei auswählen",
            Filter = "RPF-Archive (*.rpf)|*.rpf|Alle Dateien (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog() == true)
        {
            FilePath = dialog.FileName;
            await LoadRpfAsync(dialog.FileName);
        }
    }

    private async Task LoadRpfAsync(string path)
    {
        LoadingText.Visibility = Visibility.Visible;
        Vehicles.Clear();
        Warnings.Clear();

        try
        {
            var analysis = await _rpfService.AnalyzeAsync(path);

            foreach (var vehicle in analysis.Vehicles)
            {
                var vm = new RpfVehicleViewModel(vehicle);
                vm.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(RpfVehicleViewModel.IsSelected))
                    {
                        UpdateSelectionState();
                    }
                };
                Vehicles.Add(vm);
            }

            foreach (var warning in analysis.Warnings)
            {
                Warnings.Add(warning);
            }

            WarningsExpander.Visibility = Warnings.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Laden der RPF: {ex.Message}", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            LoadingText.Visibility = Visibility.Collapsed;
            UpdateSelectionState();
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vehicle in Vehicles)
        {
            vehicle.IsSelected = true;
        }
        UpdateSelectionState();
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var vehicle in Vehicles)
        {
            vehicle.IsSelected = false;
        }
        UpdateSelectionState();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var selected = Vehicles.Where(v => v.IsSelected).Select(v => v.Entry).ToList();

        if (selected.Count == 0)
        {
            MessageBox.Show("Bitte wählen Sie mindestens ein Fahrzeug aus.", "Hinweis",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        SelectedVehicles = selected;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class RpfVehicleViewModel : INotifyPropertyChanged
{
    private bool _isSelected = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            _isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public string ModelName => Entry.ModelName;
    public string? VehicleClassName => Entry.VehicleClassName;
    public int? SirenSettingId => Entry.SirenSettingId;
    public string SirenSettingIdDisplay => Entry.SirenSettingId?.ToString() ?? "-";

    public RpfVehicleEntry Entry { get; }

    public RpfVehicleViewModel(RpfVehicleEntry entry)
    {
        Entry = entry;
    }
}
