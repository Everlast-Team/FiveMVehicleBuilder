using System.Collections.ObjectModel;
using System.Windows;
using FiveMVehicleBuilder.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace FiveMVehicleBuilder.App.Views.Dialogs;

public partial class RecordEditDialog : Window
{
    public string RecordId { get; set; } = "";
    public string OriginalRecordId { get; }
    public string OriginalName { get; set; } = "";
    public string Category { get; set; } = "";
    public ObservableCollection<string> Categories { get; set; } = [];
    public ObservableCollection<VariantViewModel> Variants { get; set; } = [];
    public VariantViewModel? SelectedVariant { get; set; }
    public bool WasRenamed => RecordId != OriginalRecordId;

    // Vehicle Class
    public List<string> VehicleClasses { get; } = VehicleConstants.VehicleClasses.Select((name, idx) => $"{idx} - {name}").Prepend("(Keine)").ToList();
    public int VehicleClassIndex { get; set; }

    // Price
    public string PriceText { get; set; } = "";

    private readonly Record _record;
    private readonly HashSet<string> _existingRecordIds;
    private readonly Func<int>? _getNextSirenId;

    public RecordEditDialog(Record record, IEnumerable<string> categories, IEnumerable<string> existingRecordIds, Func<int>? getNextSirenId = null)
    {
        InitializeComponent();

        _record = record;
        _existingRecordIds = new HashSet<string>(existingRecordIds, StringComparer.OrdinalIgnoreCase);
        _getNextSirenId = getNextSirenId;
        RecordId = record.Id;
        OriginalRecordId = record.Id;
        OriginalName = record.OriginalName;
        Category = record.Category;

        foreach (var cat in categories)
        {
            Categories.Add(cat);
        }

        foreach (var variant in record.Variants)
        {
            Variants.Add(new VariantViewModel
            {
                Name = variant.Name,
                SirenSettingId = variant.SirenSettingId,
                OrgType = variant.OrgType,
                OrgCategory = variant.OrgCategory,
                DefaultLivery = variant.DefaultLivery
            });
        }

        // VehicleClass Index setzen (0 = keine, 1-22 = Klassen 0-21)
        VehicleClassIndex = record.VehicleClass.HasValue ? record.VehicleClass.Value + 1 : 0;

        // Price setzen
        PriceText = record.Price?.ToString() ?? "";

        DataContext = this;
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new InputDialog
        {
            Title = "Record umbenennen",
            Prompt = "Neuer Name für den Record:",
            InputText = RecordId,
            Owner = this
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var newId = System.Text.RegularExpressions.Regex.Replace(
                dialog.InputText.Trim().ToLowerInvariant(),
                @"[^a-z0-9_]", "_");

            if (newId == RecordId)
                return;

            if (_existingRecordIds.Contains(newId))
            {
                MessageBox.Show($"Record '{newId}' existiert bereits.", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Varianten umbenennen (alten Prefix durch neuen ersetzen)
            var oldId = RecordId;
            for (int i = 0; i < Variants.Count; i++)
            {
                var variant = Variants[i];
                if (variant.Name.StartsWith(oldId, StringComparison.OrdinalIgnoreCase))
                {
                    var suffix = variant.Name.Substring(oldId.Length);
                    Variants[i] = new VariantViewModel
                    {
                        Name = newId + suffix,
                        SirenSettingId = variant.SirenSettingId,
                        OrgType = variant.OrgType,
                        OrgCategory = variant.OrgCategory,
                        DefaultLivery = variant.DefaultLivery
                    };
                }
            }

            RecordId = newId;
            OnPropertyChanged(nameof(RecordId));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }

    private void AddVariant_Click(object sender, RoutedEventArgs e)
    {
        // Nächsten Varianten-Namen generieren (recordid + buchstabe)
        var nextLetter = (char)('a' + Variants.Count);
        var suggestedName = $"{RecordId}{nextLetter}";

        var dialog = new InputDialog
        {
            Title = "Neue Variante",
            Prompt = "Name der neuen Variante:",
            InputText = suggestedName,
            Owner = this
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            var name = dialog.InputText.Trim().ToLowerInvariant();

            if (Variants.Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show($"Variante '{name}' existiert bereits.", "Fehler",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            Variants.Add(new VariantViewModel { Name = name });
        }
    }

    private void RemoveVariant_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string variantName)
            return;

        if (Variants.Count <= 1)
        {
            MessageBox.Show("Ein Record muss mindestens eine Variante haben.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        var variant = Variants.FirstOrDefault(v => v.Name == variantName);
        if (variant != null)
        {
            Variants.Remove(variant);
        }
    }

    private void EditVariant_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not VariantViewModel variant)
            return;

        var dialog = new VariantEditDialog(variant, _getNextSirenId)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true)
        {
            // Liste aktualisieren (refresh)
            var index = Variants.IndexOf(variant);
            if (index >= 0)
            {
                Variants.RemoveAt(index);
                Variants.Insert(index, variant);
            }
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Category))
        {
            MessageBox.Show("Bitte geben Sie eine Kategorie ein.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        if (Variants.Count == 0)
        {
            MessageBox.Show("Ein Record muss mindestens eine Variante haben.", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        // Änderungen auf Record anwenden
        _record.Category = Category.Trim().ToLowerInvariant();

        // VehicleClass setzen (Index 0 = keine, 1-22 = Klassen 0-21)
        _record.VehicleClass = VehicleClassIndex > 0 ? VehicleClassIndex - 1 : null;

        // Price setzen
        if (int.TryParse(PriceText?.Trim(), out var price) && price > 0)
        {
            _record.Price = price;
        }
        else
        {
            _record.Price = null;
        }

        _record.Variants.Clear();

        foreach (var variant in Variants)
        {
            _record.Variants.Add(new Variant
            {
                Name = variant.Name,
                SirenSettingId = variant.SirenSettingId,
                OrgType = variant.OrgType,
                OrgCategory = variant.OrgCategory,
                DefaultLivery = variant.DefaultLivery
            });
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class VariantViewModel
{
    public string Name { get; set; } = "";
    public int? SirenSettingId { get; set; }
    public string? OrgType { get; set; }
    public string? OrgCategory { get; set; }
    public int? DefaultLivery { get; set; }
}
