using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using FiveMVehicleBuilder.Core.Models;

namespace FiveMVehicleBuilder.App.Views.Dialogs;

public partial class VariantEditDialog : Window, INotifyPropertyChanged
{
    private readonly VariantViewModel _variant;
    private readonly Func<int>? _getNextSirenId;

    public string VariantName { get; }
    public bool CanGenerateSirenId => _getNextSirenId != null;

    private string _sirenSettingIdText = "";
    public string SirenSettingIdText
    {
        get => _sirenSettingIdText;
        set
        {
            _sirenSettingIdText = value;
            OnPropertyChanged(nameof(SirenSettingIdText));
        }
    }
    public string DefaultLiveryText { get; set; } = "";

    private string? _orgType;
    public string? OrgType
    {
        get => _orgType;
        set
        {
            _orgType = value;
            OnPropertyChanged(nameof(OrgType));
            UpdateOrgCategories();
        }
    }

    public string? OrgCategory { get; set; }

    public ObservableCollection<string> OrgTypes { get; } = new(
        new[] { "(Keine)" }.Concat(VehicleConstants.OrgTypes));

    public ObservableCollection<string> OrgCategories { get; } = [];

    public VariantEditDialog(VariantViewModel variant, Func<int>? getNextSirenId = null)
    {
        InitializeComponent();

        _variant = variant;
        _getNextSirenId = getNextSirenId;
        VariantName = variant.Name;
        SirenSettingIdText = variant.SirenSettingId?.ToString() ?? "";
        DefaultLiveryText = variant.DefaultLivery?.ToString() ?? "";
        _orgType = string.IsNullOrEmpty(variant.OrgType) ? "(Keine)" : variant.OrgType;
        OrgCategory = variant.OrgCategory ?? "";

        UpdateOrgCategories();

        DataContext = this;
    }

    private void UpdateOrgCategories()
    {
        OrgCategories.Clear();

        if (!string.IsNullOrEmpty(_orgType) && _orgType != "(Keine)")
        {
            if (VehicleConstants.OrgCategories.TryGetValue(_orgType, out var categories))
            {
                foreach (var cat in categories)
                {
                    OrgCategories.Add(cat);
                }
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        // SirenSettingId parsen
        if (string.IsNullOrWhiteSpace(SirenSettingIdText))
        {
            _variant.SirenSettingId = null;
        }
        else if (int.TryParse(SirenSettingIdText.Trim(), out var sirenId))
        {
            _variant.SirenSettingId = sirenId;
        }
        else
        {
            MessageBox.Show("Bitte geben Sie eine gueltige Zahl fuer die Siren Setting ID ein.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // DefaultLivery parsen
        if (string.IsNullOrWhiteSpace(DefaultLiveryText))
        {
            _variant.DefaultLivery = null;
        }
        else if (int.TryParse(DefaultLiveryText.Trim(), out var livery))
        {
            _variant.DefaultLivery = livery;
        }
        else
        {
            MessageBox.Show("Bitte geben Sie eine gueltige Zahl fuer die Standard-Livery ein.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // OrgType setzen
        _variant.OrgType = _orgType == "(Keine)" ? null : _orgType;

        // OrgCategory setzen
        _variant.OrgCategory = string.IsNullOrWhiteSpace(OrgCategory) ? null : OrgCategory.Trim();

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void GenerateSirenId_Click(object sender, RoutedEventArgs e)
    {
        if (_getNextSirenId != null)
        {
            SirenSettingIdText = _getNextSirenId().ToString();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
