namespace FiveMVehicleBuilder.App.Services;

public interface INavigationService
{
    event Action<Type>? NavigationRequested;
    void NavigateTo<TViewModel>() where TViewModel : class;
    void NavigateTo(Type viewModelType);
}

public class NavigationService : INavigationService
{
    public event Action<Type>? NavigationRequested;

    public void NavigateTo<TViewModel>() where TViewModel : class
    {
        NavigateTo(typeof(TViewModel));
    }

    public void NavigateTo(Type viewModelType)
    {
        NavigationRequested?.Invoke(viewModelType);
    }
}
