using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Client.ViewModels.Core;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Features.TimeAttendanceHub.ViewModels
{
    public partial class OvertimeViewModel : ViewModelBase
    {
        private readonly IRepository<OvertimeRequest> _overtimeRepository;
        private readonly IRepository<Employee> _employeeRepository;
        private readonly INotificationService _notificationService;
        private readonly IRepository<Team> _teamRepository;
        private readonly IRepository<TeamMember> _teamMemberRepository;

        [ObservableProperty]
        private ObservableCollection<OvertimeRequest> _myRequests = new();

        [ObservableProperty]
        private ObservableCollection<Employee> _employees = new();
        
        [ObservableProperty]
        private ObservableCollection<Team> _teams = new();

        [ObservableProperty]
        private Employee? _selectedEmployee;

        [ObservableProperty]
        private Team? _selectedTeam;

        [ObservableProperty]
        private bool _isTeamRequest;

        [ObservableProperty]
        private DateTime? _date = DateTime.Today;

        [ObservableProperty]
        private TimeSpan _startTime = new TimeSpan(17, 0, 0); // 5 PM

        [ObservableProperty]
        private TimeSpan _endTime = new TimeSpan(20, 0, 0);   // 8 PM

        [ObservableProperty]
        private string _reason = string.Empty;

        [ObservableProperty]
        private bool _isSubmitting;

        public OvertimeViewModel(
            IRepository<OvertimeRequest> overtimeRepository,
            IRepository<Employee> employeeRepository,
            IRepository<Team> teamRepository,
            IRepository<TeamMember> teamMemberRepository,
            INotificationService notificationService)
        {
            _overtimeRepository = overtimeRepository;
            _employeeRepository = employeeRepository;
            _teamRepository = teamRepository;
            _teamMemberRepository = teamMemberRepository;
            _notificationService = notificationService;
             
             
            LoadDataCommand.Execute(null);
        }

        public OvertimeViewModel()
        {
             _overtimeRepository = null!;
             _employeeRepository = null!;
             _teamRepository = null!;
             _teamMemberRepository = null!;
             _notificationService = null!;
        }

        [RelayCommand]
        private async Task LoadDataAsync()
        {
            // Load Employees
            var emps = await _employeeRepository.GetAllAsync();
            Employees.Clear();
            foreach(var e in emps.OrderBy(e => e.LastName)) Employees.Add(e);
            
            if (Employees.Any()) SelectedEmployee = Employees.First();

            // Load Teams
            var teams = await _teamRepository.GetAllAsync();
            Teams.Clear();
            foreach(var t in teams.OrderBy(t => t.Name)) Teams.Add(t);
            if (Teams.Any()) SelectedTeam = Teams.First();

            await LoadRequestsAsync();
        }

        private async Task LoadRequestsAsync()
        {
             // For now load all, or filter by SelectedEmployee?
             if (SelectedEmployee == null && !IsTeamRequest) return;
             
             // If Team Request, maybe show requests for that team? 
             // Or just show "My Submitted Requests" if I am the admin?
             // Since this is a Request portal, showing All requests for context might be useful.
             // Let's stick to Current User's view or Selected Employee.
             
             var all = await _overtimeRepository.GetAllAsync();
             
             IEnumerable<OvertimeRequest> relevant;
             if (IsTeamRequest && SelectedTeam != null)
             {
                 // Find members of this team
                 var members = await _teamMemberRepository.FindAsync(tm => tm.TeamId == SelectedTeam.Id);
                 var memberIds = members.Select(m => m.EmployeeId).ToList();
                 relevant = all.Where(r => memberIds.Contains(r.EmployeeId)).OrderByDescending(r => r.Date);
             }
             else if (SelectedEmployee != null)
             {
                 relevant = all.Where(r => r.EmployeeId == SelectedEmployee.Id).OrderByDescending(r => r.Date);
             }
             else 
             {
                 relevant = Enumerable.Empty<OvertimeRequest>();
             }
             
             MyRequests.Clear();
             foreach(var r in relevant) MyRequests.Add(r);
        }
        
        partial void OnSelectedEmployeeChanged(Employee? value) 
        {
            _ = LoadRequestsAsync();
        } 

        partial void OnSelectedTeamChanged(Team? value)
        {
            _ = LoadRequestsAsync();
        }

        partial void OnIsTeamRequestChanged(bool value)
        {
            _ = LoadRequestsAsync();
        }

        [RelayCommand]
        private async Task SubmitAsync()
        {
            if (Date == null)
            {
                 await _notificationService.SendReminderAsync("Error", "Select a date.");
                 return;
            }
            if (EndTime <= StartTime)
            {
                await _notificationService.SendReminderAsync("Error", "End time must be after start time.");
                return;
            }
            if (string.IsNullOrWhiteSpace(Reason))
            {
                 await _notificationService.SendReminderAsync("Error", "Reason is required.");
                 return;
            }

            // Validation
            if (IsTeamRequest)
            {
                if (SelectedTeam == null)
                {
                    await _notificationService.SendReminderAsync("Error", "Select a team.");
                    return;
                }
            }
            else
            {
                 if (SelectedEmployee == null)
                {
                    await _notificationService.SendReminderAsync("Error", "Select an employee.");
                    return;
                }
            }

            IsSubmitting = true;
            try
            {
                if (IsTeamRequest && SelectedTeam != null)
                {
                    // Team Logic
                    // 1. Get Members
                    // 2. Create Request for each
                    
                    // Note: accessing .Members property might be empty if EF didn't include it.
                    // Safer to query TeamMembers repo or ensure Include in Team Repo.
                    // Given IRepository<Team> generic might not auto-include, let's fetch TeamMembers via repo.
                    var members = await _teamMemberRepository.FindAsync(tm => tm.TeamId == SelectedTeam.Id);
                    
                    if (!members.Any())
                    {
                         await _notificationService.SendReminderAsync("Warning", "Selected team has no members.");
                         return; // Or proceed?
                    }

                    int count = 0;
                    foreach(var member in members)
                    {
                        var request = new OvertimeRequest
                        {
                            EmployeeId = member.EmployeeId,
                            Date = Date.Value.Date,
                            StartTime = StartTime,
                            EndTime = EndTime,
                            Reason = Reason + $" (Team: {SelectedTeam.Name})",
                            Status = LeaveStatus.Pending
                        };
                        await _overtimeRepository.AddAsync(request);
                        count++;
                    }
                    
                    await _notificationService.SendReminderAsync("Success", $"Overtime Requested for {count} team members.");
                    
                    // Notify approvals to refresh
                    WeakReferenceMessenger.Default.Send(new EntityChangedMessage<OvertimeRequest>(new OvertimeRequest(), EntityChangeType.Created));
                }
                else if (SelectedEmployee != null)
                {
                    // Individual Logic
                    var request = new OvertimeRequest
                    {
                        EmployeeId = SelectedEmployee.Id,
                        Date = Date.Value.Date,
                        StartTime = StartTime,
                        EndTime = EndTime,
                        Reason = Reason,
                        Status = LeaveStatus.Pending
                    };

                    await _overtimeRepository.AddAsync(request);
                    await _notificationService.SendReminderAsync("Success", "Overtime Requested Successfully.");

                    // Notify approvals to refresh
                    WeakReferenceMessenger.Default.Send(new EntityChangedMessage<OvertimeRequest>(request, EntityChangeType.Created));
                }
                
                Reason = string.Empty;
                await LoadRequestsAsync();
            }
            catch (Exception ex)
            {
                await _notificationService.SendReminderAsync("Error", "Error: " + ex.Message);
            }
            finally
            {
                IsSubmitting = false;
            }
        }
    }
}
