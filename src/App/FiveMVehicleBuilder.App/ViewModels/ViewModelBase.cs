using CommunityToolkit.Mvvm.ComponentModel;

namespace FiveMVehicleBuilder.App.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _busyMessage;

    public virtual Task InitializeAsync() => Task.CompletedTask;
}
