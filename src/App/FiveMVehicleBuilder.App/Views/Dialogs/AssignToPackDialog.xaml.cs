using System.Collections.ObjectModel;
using System.Windows;
using FiveMVehicleBuilder.Core.Models;

namespace FiveMVehicleBuilder.App.Views.Dialogs;

public partial class AssignToPackDialog : Window
{
    public string RecordId { get; set; } = "";
    public ObservableCollection<PackInfo> Packs { get; set; } = [];
    public PackInfo? SelectedPack { get; set; }
    public bool HasCurrentPack { get; set; }
    public bool RemoveFromPack { get; private set; }

    public AssignToPackDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPack == null)
        {
            MessageBox.Show("Bitte wählen Sie ein Pack aus.", "Hinweis",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        RemoveFromPack = false;
        DialogResult = true;
        Close();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        RemoveFromPack = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class PackInfo
{
    public string Name { get; set; } = "";
    public int RecordCount { get; set; }
}
