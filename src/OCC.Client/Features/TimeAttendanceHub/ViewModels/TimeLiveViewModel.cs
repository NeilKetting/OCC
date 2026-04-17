using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OCC.Client.Services;
using OCC.Shared.Models;
using OCC.Client.ViewModels.Messages;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using System.Collections.Generic;
using Avalonia.Threading;
using Serilog;

namespace OCC.Client.Features.TimeAttendanceHub.ViewModels
{
    public partial class TimeLiveViewModel : ViewModelBase, IRecipient<UpdateStatusMessage>, IRecipient<EntityUpdatedMessage>
    {

        #region Private Members

        private readonly ITimeService _timeService;
        private readonly IAuthService _authService;
        private readonly IServiceProvider _serviceProvider;
        private readonly IDialogService _dialogService;
        private readonly IRepository<User> _userRepository;
        private readonly DispatcherTimer? _refreshTimer;

        #endregion

        #region Observables

        [ObservableProperty]
        private ObservableCollection<LiveUserCardViewModel> _liveUsers = new();

        [ObservableProperty]
        private string _lastUpdatedText = "Updated on " + DateTime.Now.ToString("dd MMMM yyyy HH:mmtt").ToLower();

        [ObservableProperty]
        private bool _isTimesheetVisible = false;

        [ObservableProperty]
        private DailyTimesheetViewModel? _currentTimesheet;

        [ObservableProperty]
        private int _selectedBranchFilterIndex = 0;

        #endregion

        #region Constructors

        public TimeLiveViewModel()
        {
            // Parameterless constructor for design-time support
            _timeService = null!;
            _authService = null!;
            _serviceProvider = null!;
            _dialogService = null!;
            _userRepository = null!;
        }

        public TimeLiveViewModel(ITimeService timeService, IAuthService authService, 
            IServiceProvider serviceProvider, IDialogService dialogService, IRepository<User> userRepository)
        {
            _timeService = timeService;
            _authService = authService;
            _serviceProvider = serviceProvider;
            _dialogService = dialogService;
            _userRepository = userRepository;
            
            InitializeCommand.Execute(null);
            WeakReferenceMessenger.Default.RegisterAll(this);

            // Setup Refresh Timer (30 seconds)
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            _refreshTimer.Tick += (s, e) => _ = LoadLiveData();
            _refreshTimer.Start();
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task Initialize()
        {
            LastUpdatedText = "Updated on " + DateTime.Now.ToString("dd MMMM yyyy HH:mmtt").ToLower();
            await LoadLiveData();
        }

        [RelayCommand]
        private void OpenTimesheet()
        {
            CurrentTimesheet = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<DailyTimesheetViewModel>(_serviceProvider);
            CurrentTimesheet.CloseRequested += (s, e) => CloseTimesheet();
            IsTimesheetVisible = true;
        }

        [RelayCommand]
        private void CloseTimesheet()
        {
            IsTimesheetVisible = false;
            CurrentTimesheet = null;
        }

        [RelayCommand]
        private async Task ClearAttendance()
        {
            try
            {
                await _timeService.ClearAllAttendanceAsync();
                // Trigger refresh via message or direct reload
                await Initialize();
            
                // Send message to notify others?
                 WeakReferenceMessenger.Default.Send(new EntityUpdatedMessage("AttendanceRecord", "ClearAll", Guid.Empty));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TimeLiveViewModel] Error Clearing Attendance: {ex.Message}");
                if (_dialogService != null) await _dialogService.ShowAlertAsync("Error", $"Failed to clear attendance: {ex.Message}");
            }
        }

        #endregion

        #region Methods

        private async Task LoadLiveData()
        {
            try
            {
                var allStaff = await _timeService.GetAllStaffAsync();
                var allStaffList = allStaff.ToList();
                
                // Fetch today's records (for historical 'today' view) AND any active records
                var today = DateTime.Today;
                var todayAttendance = await _timeService.GetDailyAttendanceAsync(today);
                var activeAttendance = await _timeService.GetActiveAttendanceAsync();
                
                var todayList = todayAttendance.ToList();
                var activeList = activeAttendance.ToList();

                // Merge and filter for records that are truly "Active" (no checkout time)
                // We treat DateTime.MinValue AND 00:00 (placeholder) as "active" if it's today.
                var mergedAttendance = todayList.Concat(activeList)
                                                      .Where(x => x.CheckOutTime == null || 
                                                                 x.CheckOutTime == DateTime.MinValue || 
                                                                 (x.Date.Date == today && x.CheckOutTime?.TimeOfDay == TimeSpan.Zero))
                                                      .GroupBy(x => x.EmployeeId ?? x.UserId)
                                                      .Select(g => g.OrderByDescending(r => r.CheckInTime ?? r.Date.Add(r.ClockInTime ?? TimeSpan.Zero)).First())
                                                      .ToList();

                // === NEW: Monthly Hours Calculation ===
                var startOfMonth = new DateTime(today.Year, today.Month, 1);
                var monthlyRecords = await _timeService.GetAttendanceByRangeAsync(startOfMonth, today);
                var monthlyRecordsList = monthlyRecords.ToList();
                
                var userViewModels = new List<LiveUserCardViewModel>();
                
                var allUsers = await _userRepository.GetAllAsync();
                var allUsersList = allUsers.ToList();
                Log.Information("[LiveView] Live data refreshed. Staff: {StaffCount}, Merged Attendance: {AttendanceCount}", allStaffList.Count, mergedAttendance.Count);

                // Only show employees that have an ACTIVE attendance record (Live means "Currently Here")
                foreach (var attendance in mergedAttendance)
                {
                    // Match failure check
                    var employee = allStaffList.FirstOrDefault(e => 
                        (attendance.EmployeeId.HasValue && e.Id == attendance.EmployeeId.Value) || 
                        (attendance.UserId.HasValue && e.LinkedUserId == attendance.UserId.Value));
                    
                    if (employee == null)
                    {
                        // FALLBACK: Try fetching by ID individually (might bypass filters/stale chunks)
                        if (attendance.EmployeeId.HasValue)
                        {
                            var directEmp = await _timeService.GetEmployeeByIdAsync(attendance.EmployeeId.Value);
                            if (directEmp != null)
                            {
                                employee = directEmp;
                            }
                        }
                    }

                    if (employee == null)
                    {
                        // Check if it's a User ID mismatch
                        var matchedUser = allUsersList.FirstOrDefault(u => u.Id == (attendance.EmployeeId ?? attendance.UserId));
                        string userHint = matchedUser != null ? $" (Found in Users table! Name: {matchedUser.DisplayName})" : " (NOT in Users table)";

                        // SILENCE: If it's an old record (not today) and doesn't match, just skip it to keep logs clean
                        if (attendance.Date.Date < DateTime.Today) continue;

                        Log.Warning("[LiveView] MATCH FAILURE: RecId={RecId}, EmpId={EmpId}, Date={Date}, In={In}, UserId={UserId}{Hint}", 
                            attendance.Id, attendance.EmployeeId, attendance.Date.ToShortDateString(), attendance.ClockInTime, attendance.UserId, userHint);
                        continue;
                    }

                    // Determine status
                    bool isPresent = attendance.Status == AttendanceStatus.Present || attendance.Status == AttendanceStatus.Late;
                    TimeSpan? clockIn = attendance.CheckInTime?.TimeOfDay ?? attendance.ClockInTime;
                    
                    bool isMidnightSentinel = attendance.CheckOutTime.HasValue && 
                                             attendance.CheckOutTime.Value.TimeOfDay.TotalSeconds < 1 &&
                                             (attendance.CheckOutTime.Value.Date == attendance.Date.Date || attendance.CheckOutTime.Value.Date == attendance.Date.AddDays(1));

                    TimeSpan? clockOut = (attendance.CheckOutTime == null || attendance.CheckOutTime == DateTime.MinValue || isMidnightSentinel) ? null : attendance.CheckOutTime.Value.TimeOfDay;

                    string FormatName(string name) => 
                        System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name?.ToLower() ?? "");

                    var vm = new LiveUserCardViewModel
                    {
                        EmployeeId = employee.Id,
                        DisplayName = $"{FormatName(employee.FirstName)}, {FormatName(employee.LastName)}"
                    };
                    vm.SetStatus(isPresent, clockIn, clockOut, employee.Branch ?? "Unknown");

                    // === Monthly Hours ===
                    var empRecords = monthlyRecordsList.Where(r => r.EmployeeId == employee.Id);
                    double totalHours = 0;
                    foreach (var record in empRecords)
                    {
                         if (record.CheckInTime.HasValue && record.CheckOutTime.HasValue && record.CheckOutTime.Value != DateTime.MinValue)
                         {
                             totalHours += (record.CheckOutTime.Value - record.CheckInTime.Value).TotalHours;
                         }
                         else if (record.CheckInTime.HasValue && 
                                 (record.CheckOutTime == null || record.CheckOutTime == DateTime.MinValue || (record.Date.Date == today && record.CheckOutTime?.TimeOfDay == TimeSpan.Zero)) && 
                                 record.Date.Date == today)
                         {
                             // Currently active session: Count hours so far
                              totalHours += (DateTime.Now - record.CheckInTime.Value).TotalHours;
                         }
                    }
                    vm.TotalMonthHoursValue = totalHours;
                    vm.TotalMonthHoursDisplay = $"{totalHours:F1}h";

                    // === Overtime & Late Logic ===
                    var dow = today.DayOfWeek;
                    bool isHoliday = false; // Placeholder
                    double multiplier = (dow == DayOfWeek.Sunday || isHoliday) ? 2.0 : (dow == DayOfWeek.Saturday ? 1.5 : 1.0);

                    TimeSpan shiftStart = employee.ShiftStartTime ?? new TimeSpan(7, 0, 0);
                    TimeSpan shiftEnd = employee.ShiftEndTime ?? (employee.Branch?.Contains("Cape") == true ? new TimeSpan(17, 0, 0) : new TimeSpan(16, 0, 0));

                    var currentTime = DateTime.Now.TimeOfDay;
                    if ((currentTime < shiftStart || currentTime > shiftEnd) && multiplier < 1.5) multiplier = 1.5;

                    if (multiplier > 1.0)
                    {
                        vm.IsOvertimeActive = true;
                        vm.OvertimeText = $"OVERTIME {multiplier:F1}x";
                        vm.OvertimeColor = multiplier >= 2.0 ? Avalonia.Media.Brushes.Red : Avalonia.Media.SolidColorBrush.Parse("#F97316");
                    }

                    if (clockIn.HasValue && clockIn.Value > shiftStart.Add(TimeSpan.FromMinutes(30)))
                    {
                        vm.IsLate = true;
                        vm.LateText = "LATE";
                    }

                    userViewModels.Add(vm);
                }

                // Apply Branch Filter
                // 0 = All, 1 = JHB, 2 = CPT
                string branchFilter = SelectedBranchFilterIndex switch
                {
                    1 => "Johannesburg",
                    2 => "Cape Town",
                    _ => "All"
                };

                var filteredUsers = branchFilter == "All" 
                    ? userViewModels.ToList() 
                    : userViewModels.Where(u => string.Equals(u.Branch?.Trim(), branchFilter, StringComparison.OrdinalIgnoreCase)).ToList();

                LastUpdatedText = "Updated on " + DateTime.Now.ToString("dd MMMM yyyy HH:mmtt").ToLower();

                Dispatcher.UIThread.Post(() =>
                {
                    LiveUsers.Clear();
                    foreach (var u in filteredUsers.OrderBy(x => x.DisplayName))
                    {
                        LiveUsers.Add(u);
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading live data: {ex.Message}");
                if (_dialogService != null)
                {
                    // Use Dispatcher to ensure UI thread if being called from background messager
                    await Dispatcher.UIThread.InvokeAsync(async () => 
                        // Don't await inside InvokeAsync lambda for the dialog result if we don't need it, 
                        // but actually we should warn user.
                        await _dialogService.ShowAlertAsync("Error", $"Critical Error loading live timesheet: {ex.Message}")
                    );
                }
            }
        }

        partial void OnSelectedBranchFilterIndexChanged(int value)
        {
            _ = LoadLiveData();
        }

        #endregion

        public void Receive(UpdateStatusMessage message)
        {
            if (message.Value == "Attendance Saved")
            {
                _ = Initialize();
            }
        }

        public void Receive(EntityUpdatedMessage message)
        {
            if (message.Value.EntityType == "AttendanceRecord")
            {
                // Real-time update from any client
                Dispatcher.UIThread.InvokeAsync(Initialize);
            }
        }

    }
}
