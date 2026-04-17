using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using OCC.Client.Features.AuthHub.ViewModels;
using OCC.Client.Mobile.Shell;
using OCC.Client.Services.Infrastructure;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Messages;
using System;
using System.Threading.Tasks;

namespace OCC.Client.Mobile.Features.Auth.Login
{
    public partial class MobileLoginViewModel : LoginViewModel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IAuthService _authService;
        private readonly IEmployeeService _employeeService;


        public MobileLoginViewModel(
            IAuthService authService, 
            LocalSettingsService localSettings, 
            ConnectionSettings connectionSettings, 
            IServiceProvider serviceProvider,
            IEmployeeService employeeService) 
            : base(authService, localSettings, connectionSettings, serviceProvider)
        {
            _authService = authService;
            _serviceProvider = serviceProvider;
            _employeeService = employeeService;
            Title = "Welcome Back";
        }

        public override async Task LoginAsync()
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
                    // Authentication successful
                    ErrorMessage = null;
                    
                    // On mobile, we want to transition to the MobileHub directly.
                    // We'll set the shell to BottomNavigation as requested.
                    var mobileHubVm = _serviceProvider.GetRequiredService<MobileHubViewModel>();
                    
                    // In a more advanced version, we'd lookup the Employee record here
                    // and pass it to the dashboard. For now, we'll trigger the hub.
                    
                    WeakReferenceMessenger.Default.Send(new NavigationMessage(mobileHubVm));
                }
            }
            catch (Exception)
            {
                ErrorMessage = "An unexpected error occurred.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
