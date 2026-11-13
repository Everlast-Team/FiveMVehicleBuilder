using System.Windows;
using FiveMVehicleBuilder.Core.Services;

namespace FiveMVehicleBuilder.App.Views.Dialogs;

public partial class ExportDialog : Window
{
    public bool IsJsonSelected { get; set; } = true;
    public bool IsLuaSelected { get; set; }
    public bool IsSqlSelected { get; set; }
    public bool IsDealershipTsSelected { get; set; }
    public bool IsOrganisationTsSelected { get; set; }

    public ExportFormat SelectedFormat
    {
        get
        {
            if (IsLuaSelected) return ExportFormat.Lua;
            if (IsSqlSelected) return ExportFormat.Sql;
            if (IsDealershipTsSelected) return ExportFormat.DealershipTs;
            if (IsOrganisationTsSelected) return ExportFormat.OrganisationTs;
            return ExportFormat.Json;
        }
    }

    public ExportDialog()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
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
