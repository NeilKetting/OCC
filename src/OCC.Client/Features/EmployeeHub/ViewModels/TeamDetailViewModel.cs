using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Client.ViewModels.Messages;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using OCC.Client.Features.TimeAttendanceHub.ViewModels;

namespace OCC.Client.Features.EmployeeHub.ViewModels
{
    public partial class TeamDetailViewModel : ViewModelBase, CommunityToolkit.Mvvm.Messaging.IRecipient<EntityUpdatedMessage>
    {
        private readonly IRepository<Team> _teamRepository;
        private readonly IRepository<TeamMember> _teamMemberRepository;
        private readonly IRepository<Employee> _employeeRepository;

        private Guid? _existingTeamId;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Title))]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        public new string Title
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Name))
                {
                    return _existingTeamId.HasValue ? "Edit Team" : "Add Team";
                }
                return Name;
            }
        }
        
        // Title Removed

        public ObservableCollection<TeamMemberDisplay> Members { get; } = new();
        
        // Setup for adding members
        public ObservableCollection<Employee> AvailableEmployees { get; } = new();
        
        [ObservableProperty]
        private Employee? _selectedEmployeeToAdd;



        public event EventHandler? CloseRequested;
        public event EventHandler? TeamSaved;

        public TeamDetailViewModel(
            IRepository<Team> teamRepository, 
            IRepository<TeamMember> teamMemberRepository,
            IRepository<Employee> employeeRepository)
        {
            _teamRepository = teamRepository;
            _teamMemberRepository = teamMemberRepository;
            _employeeRepository = employeeRepository;
            
            CommunityToolkit.Mvvm.Messaging.IMessengerExtensions.RegisterAll(CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default, this);
        }

        public void Load(Team team)
        {
            _pendingEmployeeIds.Clear();
            if (team.Id != Guid.Empty && !string.IsNullOrEmpty(team.Name))
            {
                _existingTeamId = team.Id;
                Name = team.Name;
                Description = team.Description;
                LoadMembers();
            }
            else
            {
                _existingTeamId = null;
                Name = "";
                Description = "";
                Members.Clear();
            }
            LoadEmployees();
        }

        private async void LoadMembers()
        {
            if (!_existingTeamId.HasValue) return;

            // Fetch members
            var allMembers = await _teamMemberRepository.GetAllAsync();
            var teamMembers = allMembers.Where(tm => tm.TeamId == _existingTeamId.Value).ToList();
            
            var allEmployees = await _employeeRepository.GetAllAsync(); // Cache this?

            Members.Clear();
            foreach(var tm in teamMembers)
            {
                var emp = allEmployees.FirstOrDefault(e => e.Id == tm.EmployeeId);
                if (emp != null)
                {
                    Members.Add(new TeamMemberDisplay { 
                        Id = tm.Id, 
                        Name = $"{emp.FirstName} {emp.LastName}", 
                        Role = emp.Role.ToString(),
                        EmployeeId = emp.Id
                    });
                }
            }
            RefreshAvailableEmployees();
        }

        private List<Employee> _allEmployeesCache = new();

        private async void LoadEmployees()
        {
            var emps = await _employeeRepository.GetAllAsync();
            _allEmployeesCache = emps.OrderBy(e => e.FirstName).ToList();
            RefreshAvailableEmployees();
        }

        private void RefreshAvailableEmployees()
        {
            // Get IDs of current members
            var memberIds = Members.Select(m => m.EmployeeId).ToHashSet();
            
            AvailableEmployees.Clear();
            foreach (var emp in _allEmployeesCache)
            {
                if (!memberIds.Contains(emp.Id))
                {
                    AvailableEmployees.Add(emp);
                }
            }
        }

        public void Receive(EntityUpdatedMessage message)
        {
            if (message.Value.EntityType == "TeamMember" || (message.Value.EntityType == "Team" && message.Value.Id == _existingTeamId))
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(LoadMembers);
            }
        }

        // Cache pending members for new teams (Employee IDs)
        private List<Guid> _pendingEmployeeIds = new List<Guid>();

        [RelayCommand]
        private async Task Save()
        {
            System.Diagnostics.Debug.WriteLine($"[TeamDetailViewModel] Save called. Name='{Name}', ExistingId={_existingTeamId}");

            if (string.IsNullOrWhiteSpace(Name)) 
            {
                System.Diagnostics.Debug.WriteLine("[TeamDetailViewModel] Name is empty, cancelling save.");
                return;
            }

            try
            {
                BusyText = "Saving team details...";
                IsBusy = true;
                Team team = new Team();
                if (_existingTeamId.HasValue)
                {
                    team.Id = _existingTeamId.Value;
                }
                // If new, ID is generated by new Team() (Guid.NewGuid) usually, or we set it?
                // Shared Model Team likely new Guid() in property.
                
                team.Name = Name;
                team.Description = Description;
                
                System.Diagnostics.Debug.WriteLine($"[TeamDetailViewModel] Saving team: {team.Name} (Id: {team.Id})");

                if (_existingTeamId.HasValue)
                {
                    await _teamRepository.UpdateAsync(team);
                    System.Diagnostics.Debug.WriteLine("[TeamDetailViewModel] UpdateAsync completed.");
                }
                else
                {
                    await _teamRepository.AddAsync(team);
                    System.Diagnostics.Debug.WriteLine("[TeamDetailViewModel] AddAsync completed.");

                    _existingTeamId = team.Id; // Now it exists
                    
                    // Save Pending Members
                    foreach (var empId in _pendingEmployeeIds)
                    {
                        var newMember = new TeamMember
                        {
                            TeamId = team.Id,
                            EmployeeId = empId
                        };
                        await _teamMemberRepository.AddAsync(newMember);
                    }
                    _pendingEmployeeIds.Clear();
                }

                TeamSaved?.Invoke(this, EventArgs.Empty);

                // Broadcast update so TeamManagementViewModel reloads
                var msg = new OCC.Client.ViewModels.Messages.EntityUpdatedMessage("Team", "Update", team.Id);
                CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(msg);

                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TeamDetailViewModel] Save FAILED: {ex.Message} \n {ex.StackTrace}");
                // In a real app we might want to show a dialog here
            }
            finally
            {
                IsBusy = false;
            }
        }
        
        [RelayCommand]
        private async Task AddMember()
        {
            if (SelectedEmployeeToAdd == null) return; // Allow even if existingTeamId is null
            
            // Check if already exists in UI list
            if (Members.Any(m => m.EmployeeId == SelectedEmployeeToAdd.Id)) return;

            if (_existingTeamId.HasValue)
            {
                // Immediate Save
                var newMember = new TeamMember
                {
                    TeamId = _existingTeamId.Value,
                    EmployeeId = SelectedEmployeeToAdd.Id
                };
                await _teamMemberRepository.AddAsync(newMember);
                // SignalR update will refresh list via Receive.
                // But for instant UI feedback, let's refresh list manually or wait for SignalR?
                // User wants immediate feedback. SignalR is fast but local update is better.
                // However, Receive causes LoadMembers -> RefreshAvailableEmployees.
            }
            else
            {
                // Pending Add
                _pendingEmployeeIds.Add(SelectedEmployeeToAdd.Id);
                
                // Manually update UI
                Members.Add(new TeamMemberDisplay { 
                    Id = Guid.NewGuid(), // Temporary ID
                    Name = $"{SelectedEmployeeToAdd.FirstName} {SelectedEmployeeToAdd.LastName}", 
                    Role = SelectedEmployeeToAdd.Role.ToString(),
                    EmployeeId = SelectedEmployeeToAdd.Id
                });
                
                // Refresh list immediately for Pending flow
                RefreshAvailableEmployees();
            }

            SelectedEmployeeToAdd = null; 
        }

        [RelayCommand]
        private async Task RemoveMember(TeamMemberDisplay member)
        {
            if (member == null) return;

            if (_existingTeamId.HasValue)
            {
                await _teamMemberRepository.DeleteAsync(member.Id);
                // SignalR will trigger reload
            }
            else
            {
                 // Remove from pending
                 _pendingEmployeeIds.Remove(member.EmployeeId);
                 Members.Remove(member);
                 
                 // Refresh list immediately
                 RefreshAvailableEmployees();
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    public class TeamMemberDisplay
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
        public Guid EmployeeId { get; set; }
    }
}
