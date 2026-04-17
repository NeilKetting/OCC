using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Infrastructure;
using OCC.Client.ViewModels.Core; // Added for ViewModelBase
using System.Collections.ObjectModel;
using System;

namespace OCC.Client.Features.SettingsHub.ViewModels
{
    public partial class UserPreferencesViewModel : ViewModelBase
    {
        private readonly UserActivityService _userActivityService;

        public event EventHandler? CloseRequested;

        [ObservableProperty]
        private int _selectedTimeout;

        public ObservableCollection<int> TimeoutOptions { get; } = new() { 5, 10, 15, 30, 60 };

        public UserPreferencesViewModel(UserActivityService userActivityService)
        {
            _userActivityService = userActivityService;
            
            // Load current timeout
            SelectedTimeout = (int)_userActivityService.LogoutThresholdMinutes;
            if (!TimeoutOptions.Contains(SelectedTimeout))
            {
                 TimeoutOptions.Add(SelectedTimeout);
            }
        }

        [RelayCommand]
        private void Save()
        {
            _userActivityService.UpdateTimeout(SelectedTimeout);
            Back(); // Return to previous view
        }

        [RelayCommand]
        private void Back()
        {
             CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
