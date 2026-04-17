using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Services;
using OCC.Shared.Models;
using System.Linq;

using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;

namespace OCC.Client.Features.SettingsHub.ViewModels
{
    public partial class ManageUsersViewModel : ViewModelBase
    {
        #region Private Members

        private readonly IRepository<User> _userRepository;

        #endregion

        #region Observables

        [ObservableProperty]
        private string _activeTab = "Manage Users";

        [ObservableProperty]
        private int _totalLicenses = 1;

        [ObservableProperty]
        private int _allocatedLicenses = 1;

        [ObservableProperty]
        private int _availableLicenses = 0;

        [ObservableProperty]
        private int _guests = 0;
        
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<User> _users = new();

        #endregion

        #region Constructors

        public ManageUsersViewModel()
        {
            // Parameterless constructor for design-time support
            _userRepository = null!;
        }

        public ManageUsersViewModel(IRepository<User> userRepository)
        {
            _userRepository = userRepository;
            LoadData(); 
        }

        #endregion

        #region Commands

        [RelayCommand]
        private void InvitePeople()
        {
            // Placeholder for invite functionality
        }
        
        [RelayCommand]
        private void SwitchTab(string tabName)
        {
            ActiveTab = tabName;
        }

        #endregion

        #region Methods

        public async void LoadData()
        {
             // Simulate loading users or fetch from repository
             // For now, let's create some dummy data if repository is empty or just fetch
             var users = await _userRepository.GetAllAsync();
             Users = new ObservableCollection<User>(users);

             AllocatedLicenses = Users.Count;
             AvailableLicenses = TotalLicenses - AllocatedLicenses;
             if (AvailableLicenses < 0) AvailableLicenses = 0;
        }

        #endregion
    }
}
