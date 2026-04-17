using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Shared.Models;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System;
using System.Linq;
using OCC.Client.Services.Infrastructure;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;

using OCC.Client.Features.TimeAttendanceHub.ViewModels;

namespace OCC.Client.Features.EmployeeHub.ViewModels
{
    public partial class TeamManagementViewModel : ViewModelBase, CommunityToolkit.Mvvm.Messaging.IRecipient<EntityUpdatedMessage>
    {
        private readonly IRepository<Team> _teamRepository;
        private readonly IServiceProvider _serviceProvider; 
        private readonly IDialogService _dialogService; 

        public ObservableCollection<Team> Teams { get; } = new();

        [ObservableProperty]
        private Team? _selectedTeam;
        


        [ObservableProperty]
        private string? _errorMessage;

        public event EventHandler<Team>? EditTeamRequested;

        public TeamManagementViewModel(IRepository<Team> teamRepository, IServiceProvider serviceProvider, IDialogService dialogService)
        {
            _teamRepository = teamRepository;
            _serviceProvider = serviceProvider;
            _dialogService = dialogService;
            
            LoadData(); 
            CommunityToolkit.Mvvm.Messaging.IMessengerExtensions.RegisterAll(CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default, this);
        }

        private async void LoadData()
        {
            BusyText = "Loading teams...";
            IsBusy = true;
            ErrorMessage = null;
            try 
            {
                var teams = await _teamRepository.GetAllAsync();
                Teams.Clear();
                foreach(var team in teams) Teams.Add(team);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TeamManagementViewModel] Error loading teams: {ex.Message}");
                if (_dialogService != null) await _dialogService.ShowAlertAsync("Error", $"Critical Error loading teams: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void Receive(EntityUpdatedMessage message)
        {
             if (message.Value.EntityType == "Team") 
             {
                 Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(LoadData);
             }
        }

        [RelayCommand]
        private void AddTeam()
        {
            EditTeamRequested?.Invoke(this, new Team());
        }

        [RelayCommand]
        private void EditTeam(Team team)
        {
             EditTeamRequested?.Invoke(this, team);
        }

        [RelayCommand]
        private async Task DeleteTeam(Team team)
        {
             if (team == null) return;
             
             ErrorMessage = null;
             
             try
             {
                 BusyText = $"Deleting team '{team.Name}'...";
                 IsBusy = true;
                 await _teamRepository.DeleteAsync(team.Id);
             }
             catch (System.Net.Http.HttpRequestException ex)
             {
                 // Handle specific status codes if needed
                 if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                 {
                     ErrorMessage = "Cannot delete: Team has existing members. Please remove members first.";
                     System.Diagnostics.Debug.WriteLine($"[TeamManagementViewModel] Delete Conflict: {ex.Message}");
                     if (_dialogService != null) 
                         await _dialogService.ShowAlertAsync("Deletion Failed", ErrorMessage);
                 }
                 else
                 {
                     ErrorMessage = $"Error deleting team: {ex.Message}";
                     System.Diagnostics.Debug.WriteLine($"[TeamManagementViewModel] Delete Error: {ex.Message}");
                     if (_dialogService != null) 
                         await _dialogService.ShowAlertAsync("Error", ErrorMessage);
                 }
             }
             catch (Exception ex)
             {
                 ErrorMessage = "An unexpected error occurred.";
                 System.Diagnostics.Debug.WriteLine($"[TeamManagementViewModel] General Error: {ex.Message}");
                 if (_dialogService != null) 
                      await _dialogService.ShowAlertAsync("Error", $"Unexpected error deleting team: {ex.Message}");
             }
             finally
             {
                 IsBusy = false;
             }
             // SignalR will trigger reload on success
        }
    }
}
