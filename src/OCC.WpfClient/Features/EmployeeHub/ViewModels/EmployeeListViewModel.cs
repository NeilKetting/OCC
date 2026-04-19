using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System.Collections.ObjectModel;

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

        [ObservableProperty]
        private int _selectedFilterIndex = 0; // 0 = Everyone, 1 = Permanent, 2 = Contract

        [ObservableProperty]
        private int _selectedBranchFilterIndex = 0; // 0 = All, 1 = JHB, 2 = CPT

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

        [ObservableProperty]
        private bool _isColumnPickerOpen;

        public EmployeeListViewModel(
            IEmployeeService employeeService, 
            IUserService userService, 
            IAuthService authService,
            IDialogService dialogService,
            LocalSettingsService settingsService,
            ILogger<EmployeeListViewModel> logger)
        {
            _employeeService = employeeService;
            _userService = userService;
            _authService = authService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _logger = logger;
            Title = "Employees";
            
            LoadLayout();
            _ = LoadDataAsync();
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
            var layout = new Models.EmployeeListLayout
            {
                Columns = new List<Models.ColumnConfig>
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
            OpenOverlay(new EmployeeDetailViewModel(this, employee, _employeeService, _userService, _authService, _dialogService, _logger));
        }

        [RelayCommand]
        private async Task EditEmployee(EmployeeSummaryDto? summary)
        {
            var target = summary ?? SelectedItem;
            if (target == null) return;
            
            try
            {
                IsBusy = true;
                BusyText = "Loading employee details...";
                var dto = await _employeeService.GetEmployeeAsync(target.Id);
                if (dto != null)
                {
                    var model = new Models.EmployeeModel(dto);
                    OpenOverlay(new EmployeeDetailViewModel(this, model, _employeeService, _userService, _authService, _dialogService, _logger));
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task DeleteEmployee(EmployeeSummaryDto? summary)
        {
            var target = summary ?? SelectedItem;
            if (target == null) return;
            
            var confirmed = await _dialogService.ShowConfirmationAsync("Delete Employee", 
                $"Are you sure you want to delete '{target.FirstName} {target.LastName}'? This action cannot be undone.");

            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = "Deleting employee...";
                var success = await _employeeService.DeleteEmployeeAsync(target.Id);
                if (success)
                {
                    await LoadDataAsync();
                }
            }
            finally
            {
                IsBusy = false;
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

        [RelayCommand]
        private void ToggleColumnPicker() => IsColumnPickerOpen = !IsColumnPickerOpen;

        public void CloseDetailView() => CloseOverlay();

        partial void OnSelectedFilterIndexChanged(int value) => FilterItems();
        partial void OnSelectedBranchFilterIndexChanged(int value) => FilterItems();

        protected override void FilterItems()
        {
            var filtered = _allEmployees.AsEnumerable();

            // Search Query
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var query = SearchQuery.ToLower();
                filtered = filtered.Where(e => 
                    (e.FirstName?.ToLower().Contains(query) ?? false) ||
                    (e.LastName?.ToLower().Contains(query) ?? false) ||
                    (e.EmployeeNumber?.ToLower().Contains(query) ?? false));
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

            var result = filtered.ToList();
            Items = new ObservableCollection<EmployeeSummaryDto>(result);

            // Update Stats
            TotalCount = result.Count;
            PermanentCount = result.Count(e => e.EmploymentType == EmploymentType.Permanent);
            ContractCount = result.Count(e => e.EmploymentType == EmploymentType.Contract);
        }
    }
}
