using System;
using OCC.WpfClient.Infrastructure;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface INavigationService
    {
        ViewModelBase CurrentView { get; }
        void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
        void NavigateTo(string route);
        void RegisterRoute(string route, Type viewModelType);
        Type? GetViewModelTypeForRoute(string route);
    }
}
