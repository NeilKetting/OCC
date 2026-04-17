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
    public partial class MainViewModel : ViewModelBase, IRecipient<NavigationMessage>
    {
        #region Private Members

        private readonly IServiceProvider _serviceProvider;

        #endregion

        #region Observables

        [ObservableProperty]
        private ViewModelBase _currentViewModel;


        [ObservableProperty]
        private bool _isChangeEmailVisible;

        [ObservableProperty]
        private Shared.ChangeEmailPopupViewModel? _changeEmailPopup;

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
            _currentViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();

            WeakReferenceMessenger.Default.RegisterAll(this);
        }

        #endregion

        #region Commands

        [RelayCommand]
        public void NavigateToLogin() => CurrentViewModel = _serviceProvider.GetRequiredService<LoginViewModel>();

        [RelayCommand]
        public void NavigateToRegister() => CurrentViewModel = _serviceProvider.GetRequiredService<RegisterViewModel>();

        [RelayCommand]
        public void NavigateToHome() => CurrentViewModel = _serviceProvider.GetRequiredService<ShellViewModel>();

        [RelayCommand]
        public void NavigateToMobileHub() => CurrentViewModel = _serviceProvider.GetRequiredService<MobileHubViewModel>();

        #endregion

        #region Methods

        public void Receive(NavigationMessage message)
        {
            CurrentViewModel = message.Value;
        }


        #endregion

    }
}
