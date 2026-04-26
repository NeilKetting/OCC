using System;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Features.Shell;

namespace OCC.Mobile.Services
{
    public interface INavigationService
    {
        void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
        void NavigateTo(ViewModelBase viewModel);
    }

    public class NavigationService : INavigationService
    {
        private readonly Func<MainViewModel> _mainViewModelFactory;
        private readonly IServiceProvider _serviceProvider;

        public NavigationService(Func<MainViewModel> mainViewModelFactory, IServiceProvider serviceProvider)
        {
            _mainViewModelFactory = mainViewModelFactory;
            _serviceProvider = serviceProvider;
        }

        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        {
            var viewModel = _serviceProvider.GetService(typeof(TViewModel)) as ViewModelBase;
            if (viewModel != null)
            {
                NavigateTo(viewModel);
            }
        }

        public void NavigateTo(ViewModelBase viewModel)
        {
            var mainVm = _mainViewModelFactory();
            mainVm.CurrentView = viewModel;
        }
    }
}
