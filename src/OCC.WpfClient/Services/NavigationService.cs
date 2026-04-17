using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Services
{
    public partial class NavigationService : ObservableObject, INavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Type> _routeRegistry = new();

        [ObservableProperty]
        private ViewModelBase _currentView = null!;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void RegisterRoute(string route, Type viewModelType)
        {
            _routeRegistry[route] = viewModelType;
        }

        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        {
            var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
            CurrentView = viewModel;
        }

        public void NavigateTo(string route)
        {
            var viewModelType = GetViewModelTypeForRoute(route);
            if (viewModelType != null)
            {
                var viewModel = (ViewModelBase)_serviceProvider.GetRequiredService(viewModelType);
                CurrentView = viewModel;
            }
            else
            {
                throw new ArgumentException($"Route '{route}' is not registered.", nameof(route));
            }
        }

        public Type? GetViewModelTypeForRoute(string route)
        {
            return _routeRegistry.TryGetValue(route, out var type) ? type : null;
        }
    }
}
