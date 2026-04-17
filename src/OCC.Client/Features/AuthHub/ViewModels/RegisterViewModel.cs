using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;

namespace OCC.Client.Features.AuthHub.ViewModels
{
    public partial class RegisterViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IAuthService _authService;
        private readonly IServiceProvider _serviceProvider;

        #endregion

        #region Observables

        [ObservableProperty]
        private string _firstName = string.Empty;

        [ObservableProperty]
        private string _lastName = string.Empty;

        [ObservableProperty]
        private string _companyName = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private string? _phone;

        [ObservableProperty]
        private UserRole _userRole;

        [ObservableProperty]
        private string? _errorMessage;

        #endregion

        #region Properties

        public IEnumerable<UserRole> AvailableRoles => Enum.GetValues<UserRole>();

        #endregion

        #region Constructors

        public RegisterViewModel()
        {
            // Parameterless constructor for design-time support
            _authService = null!;
            _serviceProvider = null!;
        }

        public RegisterViewModel(IAuthService authService, IServiceProvider serviceProvider)
        {
            _authService = authService;
            _serviceProvider = serviceProvider;
            UserRole = UserRole.Guest; // Default role
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task RegisterAsync()
        {
            if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName))
            {
                ErrorMessage = "Please fill in all required fields.";
                return;
            }

            var user = new User
            {
                Email = Email,
                Password = Password,
                FirstName = FirstName,
                LastName = LastName,
                Phone = Phone, 
                UserRole = UserRole
            };

            var success = await _authService.RegisterAsync(user);
            if (!success)
            {
                ErrorMessage = "Registration failed. User may already exist.";
            }
            else
            {
                // Registration successful, but account is pending approval.
                // Reset password for security
                Password = string.Empty;
                ErrorMessage = "Registration successful! Your account is pending administrator approval. Please check your email.";
                
                // Do NOT navigate to Login. Stay here so user can read message.
            }
        }

        #endregion
    }
}



