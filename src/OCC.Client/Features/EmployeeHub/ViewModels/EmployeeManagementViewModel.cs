using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection; // Added
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.ViewModels.Core;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using OCC.Client.ViewModels.Messages;
using OCC.Shared.DTOs;

using OCC.Client.Features.TimeAttendanceHub.ViewModels;

namespace OCC.Client.Features.EmployeeHub.ViewModels
{
    public partial class EmployeeManagementViewModel : ViewModelBase, CommunityToolkit.Mvvm.Messaging.IRecipient<EntityUpdatedMessage>
    {
        #region Private Members

        private readonly IEmployeeService _employeeService; // Changed from Repository
        private readonly IRepository<User> _userRepository;
        private readonly IDialogService _dialogService;
        private readonly IAuthService _authService;
        private readonly INotificationService _notificationService;
        private readonly ILeaveService _leaveService;
        private readonly IEmployeeImportService _importService;
        private readonly ITimeService _timeService;
        private readonly IExportService _exportService;
        private readonly IHolidayService _holidayService;
        private readonly IPdfService _pdfService;
        
        /// <summary>
        /// Cache for all loaded employees to support filtering without database calls
        /// </summary>
        private List<EmployeeSummaryDto> _allEmployees = new();

        #endregion

        #region Observables

        [ObservableProperty]
        private string _activeTab = "Employees";

        [ObservableProperty]
        private int _totalStaff = 0;

        public bool CanImportExport => _authService.CurrentUser?.Email?.Equals("neil@mdk.co.za", StringComparison.OrdinalIgnoreCase) ?? false;

        [ObservableProperty]
        private int _permanentCount = 0;

        [ObservableProperty]
        private int _contractCount = 0;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private ObservableCollection<EmployeeSummaryDto> _employees = new();

        [ObservableProperty]
        private int _selectedFilterIndex = 0;

        [ObservableProperty]
        private bool _isAddEmployeePopupVisible;

        [ObservableProperty]
        private EmployeeDetailViewModel? _addEmployeePopup;

        [ObservableProperty]
        private EmployeeSummaryDto? _selectedEmployee;

        [ObservableProperty]
        private int _totalCount;

        [ObservableProperty]
        private int _selectedBranchFilterIndex = 0;

        [ObservableProperty]
        private TeamManagementViewModel _teamsVM;

        [ObservableProperty]
        private bool _isAddTeamPopupVisible;

        [ObservableProperty]
        private TeamDetailViewModel? _teamDetailPopup;

        [ObservableProperty]
        private bool _isEmployeeReportPopupVisible;

        [ObservableProperty]
        private EmployeeReportViewModel? _employeeReportPopup;

        #endregion

        #region Constructors

        public EmployeeManagementViewModel()
        {
            // Designer constructor
            _employeeService = null!;
            _userRepository = null!;
            _teamsVM = null!;
            _serviceProvider = null!;
            _dialogService = null!;
            _authService = null!;
            _notificationService = null!;
            _leaveService = null!;
            _importService = null!;
            _importService = null!;
            _timeService = null!;
            _exportService = null!;
            _holidayService = null!;
            _pdfService = null!;
            CurrentContent = this!;
        }

        private readonly IServiceProvider _serviceProvider;

        public EmployeeManagementViewModel(
            IEmployeeService employeeService, 
            IRepository<User> userRepository,
            TeamManagementViewModel teamsVM,
            IServiceProvider serviceProvider,
            IDialogService dialogService,
            INotificationService notificationService,
            IAuthService authService,
            ILeaveService leaveService,
            IEmployeeImportService importService,
            ITimeService timeService,
            IExportService exportService,
            IHolidayService holidayService,
            IPdfService pdfService)
        {
            _employeeService = employeeService;
            _userRepository = userRepository;
            _teamsVM = teamsVM;
            _serviceProvider = serviceProvider;
            _dialogService = dialogService;
            _notificationService = notificationService;
            _authService = authService;
            _leaveService = leaveService;
            _importService = importService;
            _timeService = timeService;
            _exportService = exportService;
            _holidayService = holidayService;
            _pdfService = pdfService;
            
            _teamsVM.EditTeamRequested += (s, team) => 
            {
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => 
                {
                    var vm = _serviceProvider.GetRequiredService<TeamDetailViewModel>();
                    vm.Load(team);
                    vm.CloseRequested += (s2, e2) => IsAddTeamPopupVisible = false;
                    TeamDetailPopup = vm;
                    IsAddTeamPopupVisible = true;
                });
            };

            _ = LoadData();

            // Register for real-time updates
            IMessengerExtensions.RegisterAll(messenger: WeakReferenceMessenger.Default, this);

            CurrentContent = this;
        }

        public void Receive(EntityUpdatedMessage message)
        {
            if (message.Value.EntityType == "Employee")
            {
                // Refresh data on any employee change
                _ = Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(LoadData);
            }
        }

        #endregion

        #region Commands

        [RelayCommand]
        private async Task AddEmployee()
        {
             AddEmployeePopup = new EmployeeDetailViewModel(_employeeService, _userRepository, _dialogService, _authService, _leaveService, _exportService);
             AddEmployeePopup.Title = "Add New Employee";
             AddEmployeePopup.SaveButtonText = "Create Employee";
             
             await AddEmployeePopup.InitializeForNew();

             AddEmployeePopup.EmployeeAdded += (s, e) => {
                  IsAddEmployeePopupVisible = false;
                  // Refresh list?
                  _ = LoadData();
             };
             AddEmployeePopup.CloseRequested += (s, e) => IsAddEmployeePopupVisible = false;

             IsAddEmployeePopupVisible = true;
        }

        [RelayCommand]
        public virtual async Task EditEmployee(EmployeeSummaryDto employee)
        {
            if (employee == null) return;

            try 
            {
                System.Diagnostics.Debug.WriteLine($"[EmployeeManagementViewModel] Attempting to edit employee: {employee.Id} - {employee.FirstName} {employee.LastName}");
                
                AddEmployeePopup = new EmployeeDetailViewModel(_employeeService, _userRepository, _dialogService, _authService, _leaveService, _exportService);
                
                // Now await Load to ensure users are populated before binding
                await AddEmployeePopup.Load(employee.Id); // Pass ID only
                
                AddEmployeePopup.CloseRequested += (s, e) => IsAddEmployeePopupVisible = false;
                AddEmployeePopup.EmployeeAdded += (s, e) => 
                {
                    IsAddEmployeePopupVisible = false;
                    _ = LoadData(); 
                };
                IsAddEmployeePopupVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[EmployeeManagementViewModel] CRASH in EditEmployee: {ex.GetType().Name} - {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[EmployeeManagementViewModel] StackTrace: {ex.StackTrace}");
                _ = _dialogService.ShowAlertAsync("Error", $"Critical Error opening employee: {ex.Message}");
            }
        }

        [RelayCommand]
        public virtual async Task OpenEmployeeReport(EmployeeSummaryDto employee)
        {
            if (employee == null) return;

            try
            {
                // We need full details for the report
                var fullDetails = await _employeeService.GetEmployeeAsync(employee.Id); 
                if (fullDetails == null) return;

                var entity = new Employee
                {
                     Id = fullDetails.Id,
                     FirstName = fullDetails.FirstName,
                     LastName = fullDetails.LastName,
                     EmployeeNumber = fullDetails.EmployeeNumber,
                     Role = fullDetails.Role,
                     Status = fullDetails.Status,
                     EmploymentType = fullDetails.EmploymentType,
                     Branch = fullDetails.Branch,
                     // Add other fields as needed by the report
                };
                
                EmployeeReportPopup = new EmployeeReportViewModel(entity, _timeService, _exportService, _holidayService, _pdfService, _dialogService, _employeeService);
                // Optional: Subscribe to close if needed, but for now just binding IsVisible
                IsEmployeeReportPopupVisible = true;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowAlertAsync("Error", $"Could not open report: {ex.Message}");
            }
        }

        [RelayCommand]
        private void CloseReport()
        {
            IsEmployeeReportPopupVisible = false;
        }

        [ObservableProperty]
        private string? _errorMessage;

        [RelayCommand]
        public virtual async Task DeleteEmployee(EmployeeSummaryDto employee)
        {
            if (employee == null) return;
            
            ErrorMessage = null;

            try
            {
                BusyText = $"Deleting {employee.FirstName}...";
                IsBusy = true;
                await _employeeService.DeleteEmployeeAsync(employee.Id);
                await LoadData();
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    ErrorMessage = "Cannot delete employee. They may be assigned to tasks, managing projects, or in a team. Please check dependencies.";
                    CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Send(new UpdateStatusMessage(ErrorMessage));
                }
                else
                {
                    ErrorMessage = $"Error deleting employee: {ex.Message}";
                }
                System.Diagnostics.Debug.WriteLine($"[EmployeeManagementViewModel] Delete Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                ErrorMessage = "An unexpected error occurred.";
                System.Diagnostics.Debug.WriteLine($"[EmployeeManagementViewModel] General Error: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [ObservableProperty]
        private object _currentContent;

        [RelayCommand]
        private void SetActiveTab(string tabName)
        {
            ActiveTab = tabName;
            if (tabName == "Employees")
            {
                CurrentContent = this;
            }
            else if (tabName == "Teams")
            {
                CurrentContent = TeamsVM;
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
                
                string jsonString = System.Text.Json.JsonSerializer.Serialize(Employees, options);

                string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string fileName = $"OCC_Employees_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";
                string fullPath = System.IO.Path.Combine(folder, fileName);

                await System.IO.File.WriteAllTextAsync(fullPath, jsonString);

                // Notify user via toast
                WeakReferenceMessenger.Default.Send(new UpdateStatusMessage($"Backup Saved to Documents: {fileName}"));
                
                System.Diagnostics.Debug.WriteLine($"Exported to: {fullPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Export failed: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task ImportEmployees()
        {
            try
            {
                var filePath = await _dialogService.PickFileAsync("Select Employee Import CSV", new[] { "*.csv" });

                if (!string.IsNullOrEmpty(filePath))
                {
                    IsBusy = true;
                    BusyText = "Importing employees...";

                    await using var stream = System.IO.File.OpenRead(filePath);
                    var (success, failed, errors) = await _importService.ImportEmployeesAsync(stream);

                    if (failed == 0)
                    {
                        await _dialogService.ShowAlertAsync("Success", $"Successfully imported {success} employees.");
                    }
                    else
                    {
                        var errorMsg = $"Imported: {success}\nSkipped/Failed: {failed}\n\nErrors:\n" + string.Join("\n", errors.Take(10));
                        if (errors.Count > 10) errorMsg += "\n...";

                        await _dialogService.ShowAlertAsync("Import Result", errorMsg);
                    }

                    await LoadData();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Import failed: {ex.Message}");
                await _dialogService.ShowAlertAsync("Error", $"Import failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }


        #endregion

        #region Methods



        public virtual async Task LoadData()
        {
            try 
            {
                BusyText = "Loading employees...";
                IsBusy = true;
                // Capture current selection ID
                var selectedId = SelectedEmployee?.Id;

                var employees = await _employeeService.GetEmployeesAsync();
                
                // Sort by Name
                _allEmployees = employees.OrderBy(e => e.FirstName).ThenBy(e => e.LastName).ToList(); // Cache full list
                FilterEmployees();

                // Restore selection
                if (selectedId.HasValue)
                {
                    SelectedEmployee = Employees.FirstOrDefault(e => e.Id == selectedId.Value);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading employees: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        partial void OnSearchQueryChanged(string value)
        {
            FilterEmployees();
        }

        partial void OnSelectedFilterIndexChanged(int value)
        {
            FilterEmployees();
        }

        partial void OnSelectedBranchFilterIndexChanged(int value)
        {
            FilterEmployees();
        }

        partial void OnSelectedEmployeeChanged(EmployeeSummaryDto? value)
        {
            // Selection logic only, double-click handles edit now
        }

        #endregion

        #region Helper Methods

        private void FilterEmployees()
        {
            if (_allEmployees == null) return;

            var filtered = _allEmployees.AsEnumerable();

            // 1. Text Search
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(s => 
                       (s.FirstName?.ToLower().Contains(query) ?? false) ||
                       (s.LastName?.ToLower().Contains(query) ?? false) ||
                       (s.EmployeeNumber?.ToLower().Contains(query) ?? false)
                );
            }

            // 2. Type Filter
            filtered = SelectedFilterIndex switch
            {
                1 => filtered.Where(s => s.EmploymentType == EmploymentType.Permanent),
                2 => filtered.Where(s => s.EmploymentType == EmploymentType.Contract),
                _ => filtered
            };

            // 3. Branch Filter
            // 0 = All, 1 = JHB, 2 = CPT
            filtered = SelectedBranchFilterIndex switch
            {
                1 => filtered.Where(s => s.Branch == "Johannesburg"),
                2 => filtered.Where(s => s.Branch == "Cape Town"),
                _ => filtered
            };

            var resultList = filtered.ToList();
            Employees = new ObservableCollection<EmployeeSummaryDto>(resultList);

            // Update Stats based on FILTERED results
            TotalStaff = resultList.Count;
            TotalCount = resultList.Count;
            PermanentCount = resultList.Count(s => s.EmploymentType == EmploymentType.Permanent);
            ContractCount = resultList.Count(s => s.EmploymentType == EmploymentType.Contract);
        }

        #endregion
    }
}
