using System;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Features.Shell;

namespace OCC.Mobile.Services
{
    public interface INavigationService
    {
        void NavigateTo<TViewModel>() where TViewModel : ViewModelBase;
        void NavigateTo<TViewModel>(Action<TViewModel> initAction) where TViewModel : ViewModelBase;
        void NavigateTo(ViewModelBase viewModel);
        void GoBack();
        bool CanGoBack { get; }
    }

    public class NavigationService : INavigationService
    {
        private readonly Func<MainViewModel> _mainViewModelFactory;
        private readonly IServiceProvider _serviceProvider;
        private readonly System.Collections.Generic.Stack<ViewModelBase> _history = new();

        public NavigationService(Func<MainViewModel> mainViewModelFactory, IServiceProvider serviceProvider)
        {
            _mainViewModelFactory = mainViewModelFactory;
            _serviceProvider = serviceProvider;
        }

        public bool CanGoBack => _history.Count > 1;

        public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
        {
            NavigateTo<TViewModel>(null);
        }

        public void NavigateTo<TViewModel>(Action<TViewModel>? initAction) where TViewModel : ViewModelBase
        {
            var viewModel = _serviceProvider.GetService(typeof(TViewModel)) as TViewModel;
            if (viewModel != null)
            {
                initAction?.Invoke(viewModel);
                NavigateTo(viewModel);
            }
        }

        public void NavigateTo(ViewModelBase viewModel)
        {
            var mainVm = _mainViewModelFactory();
            
            // Push current view to history if it exists
            if (mainVm.CurrentView != null)
            {
                _history.Push(mainVm.CurrentView);
            }
            
            mainVm.CurrentView = viewModel;
        }

        public void GoBack()
        {
            if (_history.Count > 0)
            {
                var mainVm = _mainViewModelFactory();
                mainVm.CurrentView = _history.Pop();
            }
        }
    }
}
