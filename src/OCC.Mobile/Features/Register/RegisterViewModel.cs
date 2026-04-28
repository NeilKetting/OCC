using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.ViewModels;
using OCC.Mobile.Services;
using OCC.Shared.Models;

namespace OCC.Mobile.Features.Register
{
    public partial class RegisterViewModel : ViewModelBase
    {
        private readonly INavigationService _navigationService;
        private readonly IAuthService _authService;

        [ObservableProperty] private string _firstName = string.Empty;
        [ObservableProperty] private string _lastName = string.Empty;
        [ObservableProperty] private string _email = string.Empty;
        [ObservableProperty] private string _password = string.Empty;
        [ObservableProperty] private string _companyName = string.Empty;
        [ObservableProperty] private UserRole _selectedRole = UserRole.SiteManager;
        [ObservableProperty] private string _errorMessage = string.Empty;
        [ObservableProperty] private string _successMessage = string.Empty;

        public List<UserRole> Roles { get; } = new List<UserRole>
        {
            UserRole.Admin,
            UserRole.SiteManager,
            UserRole.Foreman,
            UserRole.ExternalContractor
        };

        public RegisterViewModel(INavigationService navigationService, IAuthService authService)
        {
            _navigationService = navigationService;
            _authService = authService;
            Title = "Register";
        }

        [RelayCommand]
        private async Task RegisterAsync()
        {
            ErrorMessage = string.Empty;
            SuccessMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(FirstName) || 
                string.IsNullOrWhiteSpace(LastName) || 
                string.IsNullOrWhiteSpace(Email) || 
                string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please fill in all required fields.";
                return;
            }

            if (SelectedRole == UserRole.ExternalContractor && string.IsNullOrWhiteSpace(CompanyName))
            {
                ErrorMessage = "Company Name is required for External Contractors.";
                return;
            }

            IsBusy = true;
            BusyText = "Registering...";

            try
            {
                var user = new User
                {
                    FirstName = FirstName,
                    LastName = LastName,
                    Email = Email,
                    Password = Password,
                    UserRole = SelectedRole,
                    CompanyName = SelectedRole == UserRole.ExternalContractor ? CompanyName : null,
                    IsApproved = false,
                    IsEmailVerified = false
                };

                var (success, createdUser, error) = await _authService.RegisterAsync(user);

                if (success)
                {
                    SuccessMessage = "Registration successful! Waiting for Admin approval.";
                    await Task.Delay(2000);
                    _navigationService.NavigateTo<OCC.Mobile.Features.Login.LoginViewModel>();
                }
                else
                {
                    ErrorMessage = error;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Error during registration: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void NavigateToLogin()
        {
            _navigationService.NavigateTo<OCC.Mobile.Features.Login.LoginViewModel>();
        }
    }
}
