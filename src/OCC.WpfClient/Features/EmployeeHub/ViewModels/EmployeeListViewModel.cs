using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace OCC.WpfClient.Features.EmployeeHub.ViewModels
{
    public partial class EmployeeListViewModel : ListViewModelBase<EmployeeSummaryDto>
    {
        private readonly IEmployeeService _employeeService;
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<EmployeeListViewModel> _logger;
        private readonly LocalSettingsService _settingsService;
        private List<EmployeeSummaryDto> _allEmployees = new();
        
        public override string ReportTitle => "Employee Directory";
        public override bool IsLandscape => true;
        public override List<ReportColumnDefinition> ReportColumns
        {
            get
            {
                var cols = new List<ReportColumnDefinition>();
                if (IsNumberVisible) cols.Add(new() { Header = "Emp #", PropertyName = "EmployeeNumber", Width = 1 });
                cols.Add(new() { Header = "First Name", PropertyName = "FirstName", Width = 1.5 });
                cols.Add(new() { Header = "Last Name", PropertyName = "LastName", Width = 1.5 });
                if (IsPositionVisible) cols.Add(new() { Header = "Position", PropertyName = "Role", Width = 2 });
                if (IsTypeVisible) cols.Add(new() { Header = "Type", PropertyName = "EmploymentType", Width = 1.2 });
                if (IsBranchVisible) cols.Add(new() { Header = "Branch", PropertyName = "Branch", Width = 1.5 });
                if (IsPhoneVisible) cols.Add(new() { Header = "Phone", PropertyName = "Phone", Width = 1.5 });
                if (IsEmailVisible) cols.Add(new() { Header = "Email", PropertyName = "Email", Width = 2.2 });
                if (IsIdNumberVisible) cols.Add(new() { Header = "ID Number", PropertyName = "IdNumber", Width = 2 });
                if (IsRateTypeVisible) cols.Add(new() { Header = "Rate Type", PropertyName = "RateType", Width = 1.2 });
                if (IsHourlyRateVisible) cols.Add(new() { Header = "Rate", PropertyName = "HourlyRate", Width = 1.2 });
                if (IsTaxNumberVisible) cols.Add(new() { Header = "Tax #", PropertyName = "TaxNumber", Width = 1.5 });
                if (IsBankNameVisible) cols.Add(new() { Header = "Bank", PropertyName = "BankName", Width = 1.5 });
                if (IsLeaveBalanceVisible) cols.Add(new() { Header = "Leave", PropertyName = "LeaveBalance", Width = 1 });
                if (IsEmploymentDateVisible) cols.Add(new() { Header = "Start Date", PropertyName = "EmploymentDate", Width = 1.5 });
                if (IsShiftStartVisible) cols.Add(new() { Header = "Shift Start", PropertyName = "ShiftStartTime", Width = 1.2 });
                if (IsShiftEndVisible) cols.Add(new() { Header = "Shift End", PropertyName = "ShiftEndTime", Width = 1.2 });
                return cols;
            }
        }

        [ObservableProperty]
        private int _selectedFilterIndex = 0; // 0 = Everyone, 1 = Permanent, 2 = Contract

        [ObservableProperty]
        private int _selectedBranchFilterIndex = 0; // 0 = All, 1 = JHB, 2 = CPT

        [ObservableProperty]
        private int _selectedStatusFilterIndex = 0; // 0 = Active Only, 1 = Inactive Only, 2 = All Statuses

        [ObservableProperty]
        private int _selectedSalaryTypeFilterIndex = 0; // 0 = All, 1 = Hourly, 2 = Monthly Salary

        [ObservableProperty] private int _permanentCount;
        [ObservableProperty] private int _contractCount;

        // Column Visibility - Core
        [ObservableProperty] private bool _isNumberVisible = true;
        [ObservableProperty] private bool _isPositionVisible = true;
        [ObservableProperty] private bool _isTypeVisible = true;
        [ObservableProperty] private bool _isBranchVisible = true;
        
        // Column Visibility - Personal
        [ObservableProperty] private bool _isPhoneVisible = false;
        [ObservableProperty] private bool _isEmailVisible = false;
        [ObservableProperty] private bool _isIdNumberVisible = false;
        
        // Column Visibility - Finance
        [ObservableProperty] private bool _isRateTypeVisible = false;
        [ObservableProperty] private bool _isHourlyRateVisible = false;
        [ObservableProperty] private bool _isTaxNumberVisible = false;
        [ObservableProperty] private bool _isBankNameVisible = false;
        
        // Column Visibility - Stats/Dates
        [ObservableProperty] private bool _isLeaveBalanceVisible = false;
        [ObservableProperty] private bool _isEmploymentDateVisible = false;
        [ObservableProperty] private bool _isShiftStartVisible = false;
        [ObservableProperty] private bool _isShiftEndVisible = false;

        

        public override IRelayCommand<object>? OpenCommand => OpenEmployeeCommand;
        public override IRelayCommand<object>? EditCommand => EditEmployeeCommand;
        public override IRelayCommand<object>? DeleteCommand => DeleteSelectedEmployeesCommand;

        private readonly ISignalRService _signalRService;

        public EmployeeListViewModel(
            IEmployeeService employeeService, 
            IUserService userService, 
            IAuthService authService,
            IDialogService dialogService,
            LocalSettingsService settingsService,
            ILogger<EmployeeListViewModel> logger,
            IPdfService pdfService,
            ISignalRService signalRService) : base(pdfService)
        {
            _employeeService = employeeService;
            _userService = userService;
            _authService = authService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _logger = logger;
            _signalRService = signalRService;
            Title = "Employees";
            
            _signalRService.OnEmployeeChanged += OnEmployeeChangedReceived;

            LoadLayout();
            _ = LoadDataAsync();
        }

        private void OnEmployeeChangedReceived(OCC.Shared.DTOs.EntityChangeDto<Employee> change)
        {
            if (change == null || change.Entity == null) return;

            App.Current.Dispatcher.Invoke(() =>
            {
                var emp = change.Entity;
                var summary = new EmployeeSummaryDto
                {
                    Id = emp.Id,
                    LinkedUserId = emp.LinkedUserId,
                    EmployeeNumber = emp.EmployeeNumber,
                    FirstName = emp.FirstName,
                    LastName = emp.LastName,
                    Email = emp.Email,
                    Phone = emp.Phone,
                    IdNumber = emp.IdNumber,
                    IdType = emp.IdType,
                    Role = emp.Role,
                    Branch = emp.Branch,
                    EmploymentType = emp.EmploymentType,
                    Status = emp.Status,
                    RateType = emp.RateType,
                    HourlyRate = emp.HourlyRate,
                    TaxNumber = emp.TaxNumber,
                    BankName = emp.BankName,
                    LeaveBalance = emp.AnnualLeaveBalance,
                    EmploymentDate = emp.EmploymentDate,
                    ShiftStartTime = emp.ShiftStartTime,
                    ShiftEndTime = emp.ShiftEndTime,
                    IsBibc = emp.IsBibc
                };

                var existing = _allEmployees.FirstOrDefault(e => e.Id == change.EntityId || e.Id == emp.Id);
                if (change.Action == "Created" || change.Action == "Create")
                {
                    if (existing == null)
                    {
                        _allEmployees.Add(summary);
                    }
                    else
                    {
                        var idx = _allEmployees.IndexOf(existing);
                        _allEmployees[idx] = summary;
                    }
                }
                else if (change.Action == "Updated" || change.Action == "Update")
                {
                    if (existing != null)
                    {
                        var idx = _allEmployees.IndexOf(existing);
                        _allEmployees[idx] = summary;
                    }
                    else
                    {
                        _allEmployees.Add(summary);
                    }
                }
                else if (change.Action == "Deleted" || change.Action == "Delete")
                {
                    if (existing != null)
                    {
                        _allEmployees.Remove(existing);
                    }
                }

                FilterItems();
            });
        }

        private void LoadLayout()
        {
            var layout = _settingsService.Settings.EmployeeListLayout;
            if (layout?.Columns != null && layout.Columns.Any())
            {
                IsNumberVisible = layout.Columns.FirstOrDefault(c => c.Header == "Number")?.IsVisible ?? true;
                IsPositionVisible = layout.Columns.FirstOrDefault(c => c.Header == "Position")?.IsVisible ?? true;
                IsTypeVisible = layout.Columns.FirstOrDefault(c => c.Header == "Type")?.IsVisible ?? true;
                IsBranchVisible = layout.Columns.FirstOrDefault(c => c.Header == "Branch")?.IsVisible ?? true;
                
                IsPhoneVisible = layout.Columns.FirstOrDefault(c => c.Header == "Phone")?.IsVisible ?? false;
                IsEmailVisible = layout.Columns.FirstOrDefault(c => c.Header == "Email")?.IsVisible ?? false;
                IsIdNumberVisible = layout.Columns.FirstOrDefault(c => c.Header == "ID Number")?.IsVisible ?? false;
                
                IsRateTypeVisible = layout.Columns.FirstOrDefault(c => c.Header == "Rate Type")?.IsVisible ?? false;
                IsHourlyRateVisible = layout.Columns.FirstOrDefault(c => c.Header == "Hourly Rate")?.IsVisible ?? false;
                IsTaxNumberVisible = layout.Columns.FirstOrDefault(c => c.Header == "Tax Number")?.IsVisible ?? false;
                IsBankNameVisible = layout.Columns.FirstOrDefault(c => c.Header == "Bank Name")?.IsVisible ?? false;
                
                IsLeaveBalanceVisible = layout.Columns.FirstOrDefault(c => c.Header == "Leave")?.IsVisible ?? false;
                IsEmploymentDateVisible = layout.Columns.FirstOrDefault(c => c.Header == "Start Date")?.IsVisible ?? false;
                IsShiftStartVisible = layout.Columns.FirstOrDefault(c => c.Header == "Shift Start")?.IsVisible ?? false;
                IsShiftEndVisible = layout.Columns.FirstOrDefault(c => c.Header == "Shift End")?.IsVisible ?? false;
            }
        }

        [RelayCommand]
        private void SaveLayout()
        {
            var layout = new OCC.WpfClient.Infrastructure.Models.ListLayout
            {
                Columns = new List<OCC.WpfClient.Infrastructure.Models.ColumnConfig>
                {
                    new() { Header = "Number", IsVisible = IsNumberVisible },
                    new() { Header = "Position", IsVisible = IsPositionVisible },
                    new() { Header = "Type", IsVisible = IsTypeVisible },
                    new() { Header = "Branch", IsVisible = IsBranchVisible },
                    new() { Header = "Phone", IsVisible = IsPhoneVisible },
                    new() { Header = "Email", IsVisible = IsEmailVisible },
                    new() { Header = "ID Number", IsVisible = IsIdNumberVisible },
                    new() { Header = "Rate Type", IsVisible = IsRateTypeVisible },
                    new() { Header = "Hourly Rate", IsVisible = IsHourlyRateVisible },
                    new() { Header = "Tax Number", IsVisible = IsTaxNumberVisible },
                    new() { Header = "Bank Name", IsVisible = IsBankNameVisible },
                    new() { Header = "Leave", IsVisible = IsLeaveBalanceVisible },
                    new() { Header = "Start Date", IsVisible = IsEmploymentDateVisible },
                    new() { Header = "Shift Start", IsVisible = IsShiftStartVisible },
                    new() { Header = "Shift End", IsVisible = IsShiftEndVisible }
                }
            };

            _settingsService.Settings.EmployeeListLayout = layout;
            _settingsService.Save();
        }

        public override async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading employees...";
                
                var employees = await _employeeService.GetEmployeesAsync();
                _allEmployees = employees.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).ToList();
                
                _logger.LogInformation("Loaded {Count} employees", _allEmployees.Count);
                FilterItems();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employees");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void AddEmployee()
        {
            var employee = new Models.EmployeeModel();
            var detailVm = new EmployeeDetailViewModel(employee, _employeeService, _userService, _authService, _dialogService, _logger, _pdfService);
            OpenOverlay(detailVm, async (res) =>
            {
                if (res is bool saved && saved)
                {
                    await LoadDataAsync();
                }
            });
        }

        [RelayCommand]
        private void BulkRaise()
        {
            var bulkRaiseVm = new BulkRaiseViewModel(_employeeService, _dialogService, _logger, _pdfService);
            OpenOverlay(bulkRaiseVm, async (res) =>
            {
                if (res is bool saved && saved)
                {
                    await LoadDataAsync();
                }
            });
        }

        [RelayCommand]
        private async Task OpenEmployee(object? parameter)
        {
            await EditEmployee(parameter);
        }

        [RelayCommand]
        private async Task EditEmployee(object? parameter)
        {
            var target = parameter as EmployeeSummaryDto ?? SelectedItem;
            if (target == null) return;
            
            try
            {
                IsBusy = true;
                BusyText = "Loading employee details...";
                var dto = await _employeeService.GetEmployeeAsync(target.Id);
                if (dto != null)
                {
                    var model = new Models.EmployeeModel(dto);
                    var detailVm = new EmployeeDetailViewModel(model, _employeeService, _userService, _authService, _dialogService, _logger, _pdfService);
                    OpenOverlay(detailVm, async (res) =>
                    {
                        if (res is bool saved && saved)
                        {
                            await LoadDataAsync();
                        }
                    });
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task FocusAndEditEmployeeAsync(Guid employeeId, string? focusSection = null)
        {
            if (_allEmployees == null || !_allEmployees.Any())
            {
                await LoadDataAsync();
            }

            var target = _allEmployees?.FirstOrDefault(e => e.Id == employeeId);
            if (target != null)
            {
                if (Items != null && !Items.Any(e => e.Id == employeeId))
                {
                    SearchQuery = string.Empty;
                    SelectedStatusFilterIndex = 2; // All Statuses
                }

                SelectedItem = Items?.FirstOrDefault(e => e.Id == employeeId) ?? target;

                try
                {
                    IsBusy = true;
                    BusyText = "Loading employee details...";
                    var dto = await _employeeService.GetEmployeeAsync(employeeId);
                    if (dto != null)
                    {
                        var model = new Models.EmployeeModel(dto);
                        var detailVm = new EmployeeDetailViewModel(model, _employeeService, _userService, _authService, _dialogService, _logger, _pdfService)
                        {
                            FocusSection = focusSection
                        };
                        OpenOverlay(detailVm, async (res) =>
                        {
                            if (res is bool saved && saved)
                            {
                                await LoadDataAsync();
                            }
                        });
                    }
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private async Task DeleteSelectedEmployees(object? parameter)
        {
            var targets = GetDeleteTargets(parameter);
            if (!targets.Any()) return;

            if (targets.Count == 1)
            {
                var target = targets[0];
                try
                {
                    IsBusy = true;
                    BusyText = "Checking database references...";
                    var refs = await _employeeService.GetEmployeeReferencesAsync(target.Id);
                    IsBusy = false;

                    bool isAdmin = _authService.CurrentUser?.UserRole == OCC.Shared.Models.UserRole.Admin;

                    if (refs != null && refs.HasReferences)
                    {
                        // Build a breakdown message of where this employee is referenced
                        var sb = new System.Text.StringBuilder();
                        sb.AppendLine($"'{target.FirstName} {target.LastName}' has the following database references:");
                        if (refs.AttendanceCount > 0) sb.AppendLine($"• Attendance Records: {refs.AttendanceCount}");
                        if (refs.TimeRecordCount > 0) sb.AppendLine($"• Time Records: {refs.TimeRecordCount}");
                        if (refs.TeamMemberCount > 0) sb.AppendLine($"• Team Membership: {refs.TeamMemberCount}");
                        if (refs.ProjectTeamMemberCount > 0) sb.AppendLine($"• Project Team Membership: {refs.ProjectTeamMemberCount}");
                        if (refs.SiteDeploymentMemberCount > 0) sb.AppendLine($"• Site Deployment Assignments: {refs.SiteDeploymentMemberCount}");
                        if (refs.LeaveRequestCount > 0) sb.AppendLine($"• Leave Requests: {refs.LeaveRequestCount}");
                        if (refs.OvertimeRequestCount > 0) sb.AppendLine($"• Overtime Requests: {refs.OvertimeRequestCount}");
                        if (refs.EmployeeLoanCount > 0) sb.AppendLine($"• Employee Loans: {refs.EmployeeLoanCount}");
                        if (refs.TaskAssignmentCount > 0) sb.AppendLine($"• Task Assignments: {refs.TaskAssignmentCount}");
                        if (refs.ClockingEventCount > 0) sb.AppendLine($"• Clocking Events: {refs.ClockingEventCount}");
                        if (refs.DailyTimesheetCount > 0) sb.AppendLine($"• Daily Timesheets: {refs.DailyTimesheetCount}");
                        if (refs.HseqTrainingCount > 0) sb.AppendLine($"• HSEQ Training Records: {refs.HseqTrainingCount}");
                        if (refs.WageRunCount > 0) sb.AppendLine($"• Wage Run Items: {refs.WageRunCount}");
                        if (refs.ProjectManagerCount > 0) sb.AppendLine($"• Project Site Manager roles: {refs.ProjectManagerCount}");

                        sb.AppendLine();

                        if (isAdmin)
                        {
                            sb.AppendLine("Deactivating will mark them as inactive. Permanently deleting will purge all references and delete the employee record from the database. (Warning: Permanent deletion cannot be undone)");
                            var choice = await _dialogService.ShowThreeButtonDialogAsync("Delete Employee Options", sb.ToString(), "Deactivate (Soft Delete)", "Delete Permanently (Hard)", "Cancel");
                            
                            if (choice == CustomDialogResult.Primary)
                            {
                                IsBusy = true;
                                BusyText = "Deactivating employee...";
                                await _employeeService.DeleteEmployeeAsync(target.Id);
                                await LoadDataAsync();
                            }
                            else if (choice == CustomDialogResult.Secondary)
                            {
                                IsBusy = true;
                                BusyText = "Permanently deleting employee and references...";
                                await _employeeService.PermanentDeleteEmployeeAsync(target.Id);
                                await LoadDataAsync();
                            }
                        }
                        else
                        {
                            sb.AppendLine("You do not have administrative permissions to permanently delete records in use. You can only Deactivate (Soft Delete) this employee.");
                            var confirmed = await _dialogService.ShowConfirmationAsync("Deactivate Employee", sb.ToString());
                            if (confirmed)
                            {
                                IsBusy = true;
                                BusyText = "Deactivating employee...";
                                await _employeeService.DeleteEmployeeAsync(target.Id);
                                await LoadDataAsync();
                            }
                        }
                    }
                    else
                    {
                        // No references found
                        string title = "Deactivate Employee";
                        string message = $"Are you sure you want to deactivate '{target.FirstName} {target.LastName}'?";
                        
                        if (isAdmin)
                        {
                            // Admins get choice of soft/hard even with 0 references
                            message = $"No database references found. Would you like to deactivate (soft delete) or permanently delete (hard delete) '{target.FirstName} {target.LastName}'?";
                            var choice = await _dialogService.ShowThreeButtonDialogAsync(title, message, "Deactivate", "Delete Permanently", "Cancel");
                            
                            if (choice == CustomDialogResult.Primary)
                            {
                                IsBusy = true;
                                BusyText = "Deactivating employee...";
                                await _employeeService.DeleteEmployeeAsync(target.Id);
                                await LoadDataAsync();
                            }
                            else if (choice == CustomDialogResult.Secondary)
                            {
                                IsBusy = true;
                                BusyText = "Permanently deleting employee...";
                                await _employeeService.PermanentDeleteEmployeeAsync(target.Id);
                                await LoadDataAsync();
                            }
                        }
                        else
                        {
                            var confirmed = await _dialogService.ShowConfirmationAsync(title, message);
                            if (confirmed)
                            {
                                IsBusy = true;
                                BusyText = "Deactivating employee...";
                                await _employeeService.DeleteEmployeeAsync(target.Id);
                                await LoadDataAsync();
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete employee");
                    await _dialogService.ShowAlertAsync("Deletion Error", ex.Message);
                }
                finally
                {
                    IsBusy = false;
                }
            }
            else
            {
                // Bulk deactivation
                string title = "Deactivate Multiple Employees";
                string message = $"You are about to make {targets.Count} employees inactive. Are you sure you want to proceed?";

                var confirmed = await _dialogService.ShowConfirmationAsync(title, message);
                if (!confirmed) return;

                try
                {
                    IsBusy = true;
                    BusyText = "Deactivating employees...";
                    foreach (var t in targets)
                    {
                        await _employeeService.DeleteEmployeeAsync(t.Id);
                    }
                    await LoadDataAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Bulk deactivation failed");
                    await _dialogService.ShowAlertAsync("Deletion Error", ex.Message);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        [RelayCommand]
        private async Task ExportEmployees()
        {
            try
            {
                if (_allEmployees == null || !_allEmployees.Any()) return;

                var options = new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                };
                
                string jsonString = System.Text.Json.JsonSerializer.Serialize(_allEmployees, options);

                string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string fileName = $"OCC_Employees_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string fullPath = System.IO.Path.Combine(folder, fileName);

                await System.IO.File.WriteAllTextAsync(fullPath, jsonString);
                _logger.LogInformation("Exported employees to {Path}", fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Export failed");
            }
        }
        [RelayCommand]
        private void OpenEmployeeReport(EmployeeSummaryDto? employee)
        {
            var target = employee ?? SelectedItem;
            if (target == null) return;
            _logger.LogInformation("Open Report requested for {Id}", target.Id);
        }

        

        partial void OnSelectedFilterIndexChanged(int value) => FilterItems();
        partial void OnSelectedBranchFilterIndexChanged(int value) => FilterItems();
        partial void OnSelectedStatusFilterIndexChanged(int value) => FilterItems();
        partial void OnSelectedSalaryTypeFilterIndexChanged(int value) => FilterItems();

        protected override void FilterItems()
        {
            var filtered = _allEmployees.AsEnumerable();

            // Search Query
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                filtered = filtered.Where(e => SearchUtils.MatchesQuery(SearchQuery, e.FirstName, e.LastName, e.EmployeeNumber, e.Role.ToString(), e.Branch));
            }

            // Employment Type Filter
            filtered = SelectedFilterIndex switch
            {
                1 => filtered.Where(e => e.EmploymentType == EmploymentType.Permanent),
                2 => filtered.Where(e => e.EmploymentType == EmploymentType.Contract),
                _ => filtered
            };

            // Branch Filter
            filtered = SelectedBranchFilterIndex switch
            {
                1 => filtered.Where(e => e.Branch == "Johannesburg"),
                2 => filtered.Where(e => e.Branch == "Cape Town"),
                _ => filtered
            };

            // Status Filter
            filtered = SelectedStatusFilterIndex switch
            {
                0 => filtered.Where(e => e.Status == EmployeeStatus.Active),
                1 => filtered.Where(e => e.Status == EmployeeStatus.Inactive || e.Status == EmployeeStatus.Terminated),
                _ => filtered
            };

            // Salary Type Filter
            filtered = SelectedSalaryTypeFilterIndex switch
            {
                1 => filtered.Where(e => e.RateType == RateType.Hourly),
                2 => filtered.Where(e => e.RateType == RateType.MonthlySalary),
                _ => filtered
            };

            var result = filtered.ToList();
            Items = new ObservableCollection<EmployeeSummaryDto>(result);

            // Update Stats
            TotalCount = result.Count;
            PermanentCount = result.Count(e => e.EmploymentType == EmploymentType.Permanent);
            ContractCount = result.Count(e => e.EmploymentType == EmploymentType.Contract);
        }

        [RelayCommand]
        public async Task PrintForeignNationalsAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Generating Foreign Nationals Passport Report...";
                
                if (_pdfService == null)
                {
                    NotifyError("Print Error", "The PDF generation service is currently unavailable.");
                    return;
                }

                var foreignNationals = Items.Where(e => e.IdType == IdType.Passport).ToList();

                var cols = new List<ReportColumnDefinition>
                {
                    new() { Header = "Emp #", PropertyName = "EmployeeNumber", Width = 1.0 },
                    new() { Header = "Name", PropertyName = "DisplayName", Width = 2.5 },
                    new() { Header = "Passport Number", PropertyName = "IdNumber", Width = 2.0 },
                    new() { Header = "Passport Stamp Date", PropertyName = "PassportStampDate", Width = 2.0 },
                    new() { Header = "Branch", PropertyName = "Branch", Width = 1.5 },
                    new() { Header = "Type", PropertyName = "EmploymentType", Width = 1.5 }
                };

                var path = await _pdfService.GenerateListReportPdfAsync("Foreign Nationals Passport Report", foreignNationals, cols, false);
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing foreign nationals report");
                NotifyError("Print Error", "An error occurred while generating the PDF report.");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
