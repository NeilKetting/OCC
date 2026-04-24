using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OCC.Client.Services.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.Client.Mobile.Features.RollCall
{
    public partial class MobileRollCallViewModel : ViewModelBase
    {
        private readonly IEmployeeService _employeeService;
        private readonly IProjectService _projectService;
        private readonly IAuthService _authService;
        private readonly Services.Repositories.Interfaces.IRepository<AttendanceRecord> _attendanceRepository;

        [ObservableProperty]
        private ObservableCollection<OCC.Shared.DTOs.ProjectSummaryDto> _assignedProjects = new();

        [ObservableProperty]
        private OCC.Shared.DTOs.ProjectSummaryDto? _selectedProject;

        [ObservableProperty]
        private ObservableCollection<EmployeeAttendanceViewModel> _crew = new();

        [ObservableProperty]
        private DateTime _selectedDate = DateTime.Today;

        public MobileRollCallViewModel(
            IEmployeeService employeeService,
            IProjectService projectService,
            IAuthService authService,
            Services.Repositories.Interfaces.IRepository<AttendanceRecord> attendanceRepository)
        {
            _employeeService = employeeService;
            _projectService = projectService;
            _authService = authService;
            _attendanceRepository = attendanceRepository;
            Title = "Daily Roll Call";
        }

        public async Task InitializeAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                
                // Load projects to filter crew
                var projects = await _projectService.GetProjectSummariesAsync();
                var user = _authService.CurrentUser;
                
                // For now, show all projects or filter if we have manager link
                AssignedProjects.Clear();
                foreach (var p in projects) AssignedProjects.Add(p);
                
                SelectedProject = AssignedProjects.FirstOrDefault();

                await LoadCrewAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task LoadCrewAsync()
        {
            if (SelectedProject == null) return;
            
            try
            {
                IsBusy = true;
                var allEmployees = await _employeeService.GetEmployeesAsync();
                
                // Get existing attendance for today
                var todayAttendance = await _attendanceRepository.GetAllAsync(); // Ideally filter by date on API
                var projectAttendance = todayAttendance.Where(a => a.Date.Date == SelectedDate.Date && a.ProjectId == SelectedProject.Id).ToList();

                Crew.Clear();
                foreach (var emp in allEmployees.Where(e => e.Status == EmployeeStatus.Active))
                {
                    var attendance = projectAttendance.FirstOrDefault(a => a.EmployeeId == emp.Id);
                    Crew.Add(new EmployeeAttendanceViewModel(emp, attendance, _attendanceRepository, SelectedProject.Id, SelectedDate));
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public partial class EmployeeAttendanceViewModel : ObservableObject
    {
        private readonly Services.Repositories.Interfaces.IRepository<AttendanceRecord> _repo;
        private readonly Guid _projectId;
        private readonly DateTime _date;

        public OCC.Shared.DTOs.EmployeeSummaryDto Employee { get; }
        
        [ObservableProperty]
        private bool _isPresent;

        private AttendanceRecord? _record;

        public EmployeeAttendanceViewModel(OCC.Shared.DTOs.EmployeeSummaryDto employee, AttendanceRecord? record, Services.Repositories.Interfaces.IRepository<AttendanceRecord> repo, Guid projectId, DateTime date)
        {
            Employee = employee;
            _record = record;
            _repo = repo;
            _projectId = projectId;
            _date = date;
            _isPresent = record?.Status == AttendanceStatus.Present;
        }

        [RelayCommand]
        private async Task ToggleAttendanceAsync()
        {
            IsPresent = !IsPresent;
            
            if (IsPresent)
            {
                if (_record == null)
                {
                    _record = new AttendanceRecord
                    {
                        EmployeeId = Employee.Id,
                        ProjectId = _projectId,
                        Date = _date,
                        Status = AttendanceStatus.Present,
                        CheckInTime = DateTime.Now
                    };
                    await _repo.AddAsync(_record);
                }
                else
                {
                    _record.Status = AttendanceStatus.Present;
                    await _repo.UpdateAsync(_record);
                }
            }
            else if (_record != null)
            {
                _record.Status = AttendanceStatus.Absent;
                await _repo.UpdateAsync(_record);
            }
        }
    }
}
