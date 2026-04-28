using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Mobile.Services;
using OCC.Mobile.ViewModels;
using OCC.Shared.Models;

namespace OCC.Mobile.Features.Dashboard
{
    public partial class TeamViewModel : ViewModelBase
    {
        private readonly ITeamService _teamService;
        private readonly INavigationService _navigationService;

        [ObservableProperty]
        private Guid _projectId;

        [ObservableProperty]
        private ObservableCollection<ProjectTeamMember> _members = new();

        public TeamViewModel(ITeamService teamService, INavigationService navigationService)
        {
            _teamService = teamService;
            _navigationService = navigationService;
            Title = "Project Team";
        }

        [RelayCommand]
        public async Task LoadData()
        {
            IsBusy = true;
            try
            {
                var members = await _teamService.GetProjectTeamAsync(ProjectId);
                Members = new ObservableCollection<ProjectTeamMember>(members);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void GoBack()
        {
            _navigationService.NavigateTo<DashboardViewModel>();
        }
    }
}
