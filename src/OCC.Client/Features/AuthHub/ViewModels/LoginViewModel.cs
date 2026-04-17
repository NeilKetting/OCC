using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OCC.Client.Services.Infrastructure;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core; // Added
using OCC.Client.ViewModels.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace OCC.Client.Features.AuthHub.ViewModels
{
    public partial class LoginViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IAuthService _authService;
        private readonly IServiceProvider _serviceProvider;
        private readonly LocalSettingsService _localSettings;
        private readonly ConnectionSettings _connectionSettings;

        #endregion

        #region Observables

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string? _errorMessage;

        #endregion

        #region Constructors
        public LoginViewModel()
        {
            // Parameterless constructor for design-time support
            _authService = null!;
            _localSettings = null!;
            _connectionSettings = null!;
            _serviceProvider = null!;
        }

        public LoginViewModel(IAuthService authService, LocalSettingsService localSettings, ConnectionSettings connectionSettings, IServiceProvider serviceProvider)
        {
            _authService = authService;
            _localSettings = localSettings;
            _connectionSettings = connectionSettings;
            _serviceProvider = serviceProvider;

            // Load saved settings
            Email = _localSettings.Settings.LastEmail;
            _connectionSettings.CustomLocalUrl = _localSettings.Settings.CustomLocalUrl;
            
            // Ensure development settings are visible during this phase.
            IsDevUser = true;

            _connectionSettings.PropertyChanged += (s, e) =>
            {
               if (e.PropertyName == nameof(ConnectionSettings.SelectedEnvironment) || 
                   e.PropertyName == nameof(ConnectionSettings.ApiBaseUrl))
               {
                   OnPropertyChanged(nameof(SelectedEnvironment));
                   OnPropertyChanged(nameof(ApiBaseUrl));
               }
            };
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
        private bool _isBusy;

        protected bool CanLogin() => !IsBusy;

        [ObservableProperty]
        private bool _isDevUser;

        // [ObservableProperty]
        // private bool _useLocalDb; // Removed in favor of Enum

        public ConnectionSettings.AppEnvironment SelectedEnvironment
        {
            get => _connectionSettings.SelectedEnvironment;
            set
            {
                if (_connectionSettings.SelectedEnvironment != value)
                {
                    _connectionSettings.SelectedEnvironment = value;
                    OnPropertyChanged();
                }
            }
        }

        public List<ConnectionSettings.AppEnvironment> Environments { get; } = Enum.GetValues<ConnectionSettings.AppEnvironment>().ToList();

        public string? CustomLocalUrl
        {
            get => _connectionSettings.CustomLocalUrl;
            set
            {
                if (_connectionSettings.CustomLocalUrl != value)
                {
                    _connectionSettings.CustomLocalUrl = value;
                    _localSettings.Settings.CustomLocalUrl = value;
                    _localSettings.Save();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ApiBaseUrl));
                }
            }
        }

        public string ApiBaseUrl => _connectionSettings.ApiBaseUrl;
#endregion

        #region Commands

        [RelayCommand(CanExecute = nameof(CanLogin))]
        public virtual async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                ErrorMessage = "Email is required.";
                return;
            }

            try
            {
                IsBusy = true;
                var (success, errorMessage) = await _authService.LoginAsync(Email, Password);
                if (!success)
                {
                    ErrorMessage = string.IsNullOrEmpty(errorMessage) ? "Invalid email or password." : errorMessage;
                }
                else
                {
                    // Save email on successful login
                    _localSettings.Settings.LastEmail = Email;
                    _localSettings.Save();

                    ErrorMessage = null;
                    var shellViewModel = _serviceProvider.GetRequiredService<ShellViewModel>();
                    WeakReferenceMessenger.Default.Send(new NavigationMessage(shellViewModel));
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        #endregion
    }
}



