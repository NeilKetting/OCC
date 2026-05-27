using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.ViewModels.Messages;
using OCC.Client.Features.HomeHub.ViewModels;
using OCC.Client.Features.ProjectsHub.ViewModels;
using OCC.Client.Features.EmployeeHub.ViewModels;
using OCC.Client.Features.TimeAttendanceHub.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using OCC.Client.Services;
using OCC.Shared.Models;
using System;
using Avalonia.Threading;

using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Infrastructure;
using OCC.Client.Features.AuthHub.ViewModels; // Added
using OCC.Client.Mobile.Shell;

namespace OCC.Client.ViewModels.Core
{
    /// <summary>
    /// The MainViewModel for the core cross-platform desktop/mobile client.
    /// Manages top-level navigation routes and controls the currently displayed ViewModel.
    /// </summary>
    public partial class MainViewModel : ViewModelBase, IRecipient<NavigationMessage>
    {
        #region Private Members

        private readonly IServiceProvider _serviceProvider; // Injected service provider to resolve view models dynamically

        #endregion

        #region Observables

        [ObservableProperty]
        private ViewModelBase _currentViewModel; // The active view model presented in the main window content area


        [ObservableProperty]
        private bool _isChangeEmailVisible; // Toggles visibility of the change email overlay/dialog

        [ObservableProperty]
        private Shared.ChangeEmailPopupViewModel? _changeEmailPopup; // Context for the change email popup overlay

        #endregion

        #region Constructors

        public MainViewModel()
        {
            // Parameterless constructor for design-time support                
            _serviceProvider = null!;
            _currentViewModel = null!;

            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        public MainViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _currentViewModel = _serviceProvider.GetRequiredService<LoginViewModel>(); // Default to Login view on load

            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        #endregion

        #region Commands

        /// <summary>
        /// Navigates the main display to the Login ViewModel.
        /// </summary>
        [RelayCommand]
        public void NavigateToLogin() => CurrentViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();

        /// <summary>
        /// Navigates the main display to the Register ViewModel.
        /// </summary>
        [RelayCommand]
        public void NavigateToRegister() => CurrentViewModel = _serviceProvider.GetRequiredService<RegisterViewModel>();

        /// <summary>
        /// Navigates the main display to the main application Shell (Home) ViewModel.
        /// </summary>
        [RelayCommand]
        public void NavigateToHome() => CurrentViewModel = _serviceProvider.GetRequiredService<ShellViewModel>();

        /// <summary>
        /// Navigates the main display to the Mobile Hub ViewModel.
        /// </summary>
        [RelayCommand]
        public void NavigateToMobileHub() => CurrentViewModel = _serviceProvider.GetRequiredService<MobileHubViewModel>();

        #endregion

        #region Methods

        /// <summary>
        /// Handles incoming navigation messages to switch the active ViewModel context.
        /// </summary>
        public void Receive(NavigationMessage message)
        {
            CurrentViewModel = message.Value;
        }


        #endregion

    }
}
