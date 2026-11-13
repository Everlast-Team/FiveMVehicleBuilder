using System.Windows;
using FiveMVehicleBuilder.App.Services;
using FiveMVehicleBuilder.App.ViewModels;

namespace FiveMVehicleBuilder.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var navigationService = new NavigationService();
        var dialogService = new DialogService();

        var viewModel = new MainViewModel(navigationService, dialogService);
        DataContext = viewModel;

        Loaded += async (s, e) =>
        {
            await viewModel.InitializeAsync();
        };
    }
}
