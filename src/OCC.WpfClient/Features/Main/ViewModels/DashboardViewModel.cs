using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Features.Main.ViewModels
{
    public partial class DashboardViewModel : ViewModelBase
    {
        private readonly IUserService _userService;
        private readonly IToastService _toastService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string _userName = "User";

        [ObservableProperty]
        private int _taskCount = 3;

        [ObservableProperty]
        private int _todoCount = 1;

        [ObservableProperty]
        private int _userCount;

        [ObservableProperty]
        private string _currentDate = DateTime.Now.ToString("dd MMMM yyyy");

        [ObservableProperty]
        private string _greeting = "Good afternoon";

        public DashboardViewModel(IUserService userService, IToastService toastService, IAuthService authService)
        {
            _userService = userService;
            _toastService = toastService;
            _authService = authService;
            Title = "Dashboard";
            
            UserName = _authService.CurrentUser?.DisplayName ?? "User";
            _ = LoadData();
        }

        private async System.Threading.Tasks.Task LoadData()
        {
            try
            {
                var users = await _userService.GetUsersAsync();
                UserCount = users.Count();

                _toastService.ShowSuccess("System Active", "Toast Notification System is now live!");
            }
            catch { /* Ignore */ }
        }
    }
}
