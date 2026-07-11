using CommunityToolkit.Mvvm.ComponentModel;
using OCC.WpfClient.Services.Interfaces;
using OCC.Shared.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.Main.ViewModels
{
    public partial class BirthdayWidgetViewModel : WidgetViewModelBase
    {
        private readonly IEmployeeService _employeeService;
        private readonly IAuthService _authService;

        [ObservableProperty]
        private string _birthdayGreeting = string.Empty;

        public BirthdayWidgetViewModel(IEmployeeService employeeService, IAuthService authService)
        {
            _employeeService = employeeService;
            _authService = authService;
            WidgetId = "Birthday";
            Title = "Birthday Greeting";
        }

        public override async Task RefreshDataAsync()
        {
            try
            {
                var today = DateTime.Today;
                var employees = await _employeeService.GetEmployeesAsync();

                // Check if today is the CURRENT LOGGED-IN user's birthday!
                var currentUserId = _authService.CurrentUser?.Id;
                var selfEmployee = employees.FirstOrDefault(e => e.Status == EmployeeStatus.Active && e.LinkedUserId == currentUserId);
                if (selfEmployee != null && selfEmployee.DoB.Month == today.Month && selfEmployee.DoB.Day == today.Day)
                {
                    int age = today.Year - selfEmployee.DoB.Year;
                    if (selfEmployee.DoB.Date > today.AddYears(-age)) age--;

                    var random = new Random();
                    var selfMessages = new[]
                    {
                        $"Happy {age}th Birthday! We wish you a fantastic day ahead filled with joy and success! 🎉🎂",
                        $"Congratulations on turning {age}! Have a wonderful birthday! 🥳🧁",
                        $"Happy Birthday! Cheers to {age} years of greatness! 🥂🎉",
                        $"Happy {age}th Birthday from everyone here at OCC! Hope your day is amazing! 🎈🎁"
                    };
                    BirthdayGreeting = selfMessages[random.Next(selfMessages.Length)];
                    IsVisible = true;
                }
                else
                {
                    BirthdayGreeting = string.Empty;
                    IsVisible = false; // Hide if no birthday greeting
                }
            }
            catch { }
        }
    }
}
