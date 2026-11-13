using System.Windows;

namespace FiveMVehicleBuilder.App.Views.Dialogs;

public partial class InputDialog : Window
{
    public new string Title
    {
        get => base.Title;
        set => base.Title = value;
    }

    public string Prompt { get; set; } = "";
    public string InputText { get; set; } = "";

    public InputDialog()
    {
        InitializeComponent();
        Title = "Eingabe";
        DataContext = this;
        Loaded += (_, _) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
