using System.Collections.ObjectModel;
using System.Windows;

namespace FiveMVehicleBuilder.App.Views.Dialogs;

public partial class SelectionDialog : Window
{
    public new string Title
    {
        get => base.Title;
        set => base.Title = value;
    }

    public string Prompt { get; set; } = "";
    public ObservableCollection<string> Items { get; set; } = [];
    public string? SelectedItem { get; set; }

    public SelectionDialog()
    {
        InitializeComponent();
        Title = "Auswahl";
        DataContext = this;
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedItem == null)
        {
            MessageBox.Show("Bitte wählen Sie einen Eintrag aus.", "Hinweis",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
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
