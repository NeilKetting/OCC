using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System.Collections.ObjectModel;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    /// <summary>
    /// Manages Work Teams – groups of employees used for fast mobile clock-in.
    /// Supports team CRUD and managing members within each team.
    /// </summary>
    public partial class TeamManagementViewModel : ListViewModelBase<Team>
    {
        private readonly IAttendanceService _attendanceService;
        private readonly IEmployeeService _employeeService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<TeamManagementViewModel> _logger;

        public override string ReportTitle => "Work Teams Directory";
        public override List<ReportColumnDefinition> ReportColumns => new()
        {
            new() { Header = "Team Name", PropertyName = "Name", Width = 2 },
            new() { Header = "Description", PropertyName = "Description", Width = 3 },
            new() { Header = "Members", PropertyName = "Members.Count", Width = 1 },
        };

        public override IRelayCommand<object>? OpenCommand => OpenTeamCommand;
        public override IRelayCommand<object>? DeleteCommand => DeleteTeamCommand;

        private List<Team> _allTeams = new();

        [ObservableProperty] private Team? _selectedTeam;
        [ObservableProperty] private bool _isDetailPanelOpen;
        [ObservableProperty] private bool _isCreating;
        [ObservableProperty] private string _editName = string.Empty;
        [ObservableProperty] private string _editDescription = string.Empty;

        // Team member management
        [ObservableProperty] private ObservableCollection<TeamMemberRow> _teamMembers = new();
        [ObservableProperty] private ObservableCollection<EmployeeSummaryDto> _availableEmployees = new();
        [ObservableProperty] private EmployeeSummaryDto? _selectedAvailableEmployee;

        private List<EmployeeSummaryDto> _allEmployees = new();

        public TeamManagementViewModel(
            IAttendanceService attendanceService,
            IEmployeeService employeeService,
            IDialogService dialogService,
            IPdfService pdfService,
            ILogger<TeamManagementViewModel> logger) : base(pdfService)
        {
            _attendanceService = attendanceService;
            _employeeService = employeeService;
            _dialogService = dialogService;
            _logger = logger;
            Title = "Team Management";
        }

        public override async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading teams...";
                _allTeams = (await _attendanceService.GetTeamsAsync()).ToList();
                _allEmployees = (await _employeeService.GetEmployeesAsync()).ToList();
                FilterItems();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading teams");
            }
            finally { IsBusy = false; }
        }

        protected override void FilterItems()
        {
            var filtered = _allTeams.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var q = SearchQuery.ToLower();
                filtered = filtered.Where(t =>
                    t.Name.ToLower().Contains(q) ||
                    t.Description.ToLower().Contains(q));
            }
            var result = filtered.OrderBy(t => t.Name).ToList();
            Items = new ObservableCollection<Team>(result);
            TotalCount = result.Count;
        }

        [RelayCommand]
        private void AddTeam()
        {
            IsCreating = true;
            SelectedTeam = null;
            EditName = string.Empty;
            EditDescription = string.Empty;
            TeamMembers.Clear();
            IsDetailPanelOpen = true;
        }

        [RelayCommand]
        private async Task OpenTeam(object? parameter)
        {
            var team = parameter as Team ?? SelectedItem;
            if (team == null) return;

            // Reload from API to get fresh members
            var fresh = await _attendanceService.GetTeamAsync(team.Id);
            if (fresh == null) return;

            IsCreating = false;
            SelectedTeam = fresh;
            EditName = fresh.Name;
            EditDescription = fresh.Description;
            RefreshMemberList(fresh);
            IsDetailPanelOpen = true;
        }

        private void RefreshMemberList(Team team)
        {
            var memberIds = team.Members.Select(m => m.EmployeeId).ToHashSet();
            TeamMembers = new ObservableCollection<TeamMemberRow>(
                _allEmployees
                    .Where(e => memberIds.Contains(e.Id))
                    .Select(e => new TeamMemberRow { EmployeeId = e.Id, Name = $"{e.FirstName} {e.LastName}", Role = e.Role.ToString() }));

            AvailableEmployees = new ObservableCollection<EmployeeSummaryDto>(
                _allEmployees.Where(e => !memberIds.Contains(e.Id)).OrderBy(e => e.FirstName));
        }

        [RelayCommand]
        private async Task SaveTeam()
        {
            if (string.IsNullOrWhiteSpace(EditName))
            {
                NotifyError("Validation", "Team name is required.");
                return;
            }
            try
            {
                IsBusy = true;
                if (IsCreating)
                {
                    var newTeam = new Team { Name = EditName, Description = EditDescription };
                    var created = await _attendanceService.CreateTeamAsync(newTeam);
                    if (created != null)
                    {
                        SelectedTeam = created;
                        IsCreating = false;
                        NotifySuccess("Created", $"Team '{EditName}' created successfully.");
                    }
                }
                else if (SelectedTeam != null)
                {
                    SelectedTeam.Name = EditName;
                    SelectedTeam.Description = EditDescription;
                    await _attendanceService.UpdateTeamAsync(SelectedTeam);
                    NotifySuccess("Saved", $"Team '{EditName}' updated.");
                }
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving team");
                NotifyError("Save Failed", ex.Message);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private async Task AddMember()
        {
            if (SelectedTeam == null || SelectedAvailableEmployee == null) return;
            try
            {
                await _attendanceService.AddTeamMemberAsync(SelectedTeam.Id, SelectedAvailableEmployee.Id);
                var fresh = await _attendanceService.GetTeamAsync(SelectedTeam.Id);
                if (fresh != null) RefreshMemberList(fresh);
                NotifySuccess("Member Added", $"{SelectedAvailableEmployee.FirstName} added to team.");
                SelectedAvailableEmployee = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding member");
                NotifyError("Error", ex.Message);
            }
        }

        [RelayCommand]
        private async Task RemoveMember(TeamMemberRow? row)
        {
            if (SelectedTeam == null || row == null) return;
            try
            {
                await _attendanceService.RemoveTeamMemberAsync(SelectedTeam.Id, row.EmployeeId);
                var fresh = await _attendanceService.GetTeamAsync(SelectedTeam.Id);
                if (fresh != null) RefreshMemberList(fresh);
                NotifySuccess("Member Removed", $"{row.Name} removed from team.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member");
                NotifyError("Error", ex.Message);
            }
        }

        [RelayCommand]
        private async Task DeleteTeam(object? parameter)
        {
            var team = parameter as Team ?? SelectedItem;
            if (team == null) return;

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                "Delete Team",
                $"Are you sure you want to delete team '{team.Name}'? All member links will be removed.");
            if (!confirmed) return;

            try
            {
                IsBusy = true;
                await _attendanceService.DeleteTeamAsync(team.Id);
                NotifySuccess("Deleted", $"Team '{team.Name}' deleted.");
                IsDetailPanelOpen = false;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting team");
                NotifyError("Delete Failed", ex.Message);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        private void CloseDetail()
        {
            IsDetailPanelOpen = false;
            SelectedTeam = null;
        }
    }

    public class TeamMemberRow
    {
        public Guid EmployeeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
    }
}
