using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    public partial class LoansManagementViewModel : OverlayHostViewModel
    {
        private readonly IEmployeeLoanService _loanService;
        private readonly IEmployeeService _employeeService;
        private readonly IPdfService _pdfService;
        private readonly ILogger<LoansManagementViewModel> _logger;

        [ObservableProperty] private ObservableCollection<EmployeeLoan> _loans = new();
        partial void OnLoansChanged(ObservableCollection<EmployeeLoan> value) => OnPropertyChanged(nameof(LoansView));
        [ObservableProperty] private EmployeeLoan? _selectedLoan;
        [ObservableProperty] private bool _isAddPanelVisible;

        // AddLoan form fields
        [ObservableProperty] private ObservableCollection<EmployeeSummaryDto> _employees = new();
        [ObservableProperty] private EmployeeSummaryDto? _selectedEmployee;
        [ObservableProperty] private decimal _principalAmount;
        [ObservableProperty] private decimal _monthlyInstallment;
        [ObservableProperty] private decimal _interestRate = 0m;
        [ObservableProperty] private DateTime _loanStartDate = DateTime.Today;
        [ObservableProperty] private string _loanNotes = string.Empty;
        [ObservableProperty] private string _selectedPaymentFrequency = "Fortnightly";
        [ObservableProperty] private int _numberOfInstallments = 10;

        // Computed repayment preview
        public decimal TotalRepayableAmount => PrincipalAmount + (PrincipalAmount * InterestRate / 100);
        public string RepaymentDurationText => $"{NumberOfInstallments} {SelectedPaymentFrequency}";
 
        partial void OnPrincipalAmountChanged(decimal value)     { RecalculateInstallment(); OnPropertyChanged(nameof(TotalRepayableAmount)); OnPropertyChanged(nameof(RepaymentDurationText)); }
        partial void OnInterestRateChanged(decimal value)        { RecalculateInstallment(); OnPropertyChanged(nameof(TotalRepayableAmount)); OnPropertyChanged(nameof(RepaymentDurationText)); }
        partial void OnSelectedPaymentFrequencyChanged(string value) { RecalculateInstallment(); OnPropertyChanged(nameof(TotalRepayableAmount)); OnPropertyChanged(nameof(RepaymentDurationText)); }
        partial void OnNumberOfInstallmentsChanged(int value)    { RecalculateInstallment(); OnPropertyChanged(nameof(TotalRepayableAmount)); OnPropertyChanged(nameof(RepaymentDurationText)); }
        partial void OnSelectedEmployeeChanged(EmployeeSummaryDto? value)  { OnPropertyChanged(nameof(RepaymentDurationText)); }

        public LoansManagementViewModel(
            IEmployeeLoanService loanService,
            IEmployeeService employeeService,
            IPdfService pdfService,
            ILogger<LoansManagementViewModel> logger)
        {
            _loanService = loanService;
            _employeeService = employeeService;
            _pdfService = pdfService;
            _logger = logger;
            Title = "Loans Management";
        }

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading loans...";
                var loans = await _loanService.GetAllAsync();
                Loans = new ObservableCollection<EmployeeLoan>(loans.OrderBy(l => l.Employee?.LastName));

                var employees = await _employeeService.GetEmployeesAsync();
                Employees = new ObservableCollection<EmployeeSummaryDto>(
                    employees.Where(e => e.Status == EmployeeStatus.Active).OrderBy(e => e.LastName));
                if (Employees.Any()) SelectedEmployee = Employees.First();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading loans");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public void ShowAddPanel() => IsAddPanelVisible = true;

        [RelayCommand]
        public void CancelAdd()
        {
            IsAddPanelVisible = false;
            ResetAddForm();
        }

        public override void CloseOverlay()
        {
            CancelAdd();
            base.CloseOverlay();
        }

        [RelayCommand]
        public async Task SaveLoanAsync()
        {
            if (SelectedEmployee == null || PrincipalAmount <= 0 || MonthlyInstallment <= 0) return;

            try
            {
                IsBusy = true;
                BusyText = "Saving loan...";
                var loan = new EmployeeLoan
                {
                    EmployeeId         = SelectedEmployee.Id,
                    PrincipalAmount    = PrincipalAmount,
                    OutstandingBalance = TotalRepayableAmount,
                    MonthlyInstallment = MonthlyInstallment,
                    InterestRate       = InterestRate,
                    StartDate          = LoanStartDate,
                    IsActive           = true,
                    Notes              = $"[Term: {SelectedPaymentFrequency}, Installments: {NumberOfInstallments}] {LoanNotes}".Trim()
                };
                await _loanService.AddAsync(loan);
                IsAddPanelVisible = false;
                ResetAddForm();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving loan");
                System.Windows.MessageBox.Show($"Failed to save loan:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task SaveAndPrintLoanAsync()
        {
            if (SelectedEmployee == null || PrincipalAmount <= 0 || MonthlyInstallment <= 0) return;

            try
            {
                IsBusy = true;
                BusyText = "Saving loan...";
                var loan = new EmployeeLoan
                {
                    EmployeeId         = SelectedEmployee.Id,
                    PrincipalAmount    = PrincipalAmount,
                    OutstandingBalance = TotalRepayableAmount,
                    MonthlyInstallment = MonthlyInstallment,
                    InterestRate       = InterestRate,
                    StartDate          = LoanStartDate,
                    IsActive           = true,
                    Notes              = $"[Term: {SelectedPaymentFrequency}, Installments: {NumberOfInstallments}] {LoanNotes}".Trim()
                };
                
                var savedLoan = await _loanService.AddAsync(loan);
                
                var emp = SelectedEmployee;
                
                IsAddPanelVisible = false;
                ResetAddForm();
                await LoadDataAsync();

                if (savedLoan != null)
                {
                    savedLoan.Employee = new Employee
                    {
                        FirstName = emp.FirstName,
                        LastName = emp.LastName,
                        EmployeeNumber = emp.EmployeeNumber
                    };

                    IsBusy = true;
                    BusyText = "Generating Agreement PDF...";
                    var path = await _pdfService.GenerateLoanSchedulePdfAsync(savedLoan, savedLoan.Employee);
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving and printing loan");
                System.Windows.MessageBox.Show($"Failed to save and print loan:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task TerminateLoanAsync(EmployeeLoan? loan)
        {
            if (loan == null) return;
            var result = System.Windows.MessageBox.Show(
                $"Mark loan for {loan.Employee?.DisplayName} as fully paid / terminated?",
                "Terminate Loan", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                loan.IsActive = false;
                await _loanService.UpdateAsync(loan);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error terminating loan");
                System.Windows.MessageBox.Show($"Failed to update loan:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        public async Task PrintLoanScheduleAsync(EmployeeLoan? loan)
        {
            if (loan?.Employee == null) return;
            try
            {
                IsBusy = true;
                BusyText = "Generating Agreement PDF...";
                var path = await _pdfService.GenerateLoanSchedulePdfAsync(loan, loan.Employee);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing loan agreement");
                System.Windows.MessageBox.Show($"Failed to generate PDF:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task PrintLoanStatementAsync(EmployeeLoan? loan)
        {
            if (loan == null) return;
            try
            {
                IsBusy = true;
                BusyText = "Generating Statement PDF...";
                var statement = await _loanService.GetStatementAsync(loan.Id);
                if (statement == null)
                {
                    System.Windows.MessageBox.Show("Failed to retrieve statement data.", "Error",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    return;
                }
                var path = await _pdfService.GenerateLoanStatementPdfAsync(statement);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing loan statement");
                System.Windows.MessageBox.Show($"Failed to generate PDF:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private void ResetAddForm()
        {
            PrincipalAmount    = 0;
            MonthlyInstallment = 0;
            InterestRate       = 0;
            LoanStartDate      = DateTime.Today;
            LoanNotes          = string.Empty;
            SelectedPaymentFrequency = "Fortnightly";
            NumberOfInstallments = 10;
            if (Employees.Any()) SelectedEmployee = Employees.First();
        }

        private void RecalculateInstallment()
        {
            if (PrincipalAmount <= 0 || NumberOfInstallments <= 0) return;

            decimal totalRepayable = PrincipalAmount + (PrincipalAmount * InterestRate / 100);
            MonthlyInstallment = Math.Round(totalRepayable / NumberOfInstallments, 2);
        }

        // ─── Search and Filtering ──────────────────────────────────────────

        [ObservableProperty] private string _searchQuery = string.Empty;
        [ObservableProperty] private string _branchFilter = "All";
        [ObservableProperty] private string _statusFilter = "All";
        [ObservableProperty] private bool _isAdvancedFiltersVisible;
        
        [ObservableProperty] private decimal? _minPrincipal;
        [ObservableProperty] private decimal? _maxPrincipal;
        [ObservableProperty] private decimal? _minInstallment;
        [ObservableProperty] private decimal? _maxInstallment;
        [ObservableProperty] private decimal? _minBalance;
        [ObservableProperty] private decimal? _maxBalance;
        [ObservableProperty] private decimal? _minInterestRate;
        [ObservableProperty] private decimal? _maxInterestRate;
        [ObservableProperty] private DateTime? _startDateFrom;
        [ObservableProperty] private DateTime? _startDateTo;

        public ICollectionView LoansView
        {
            get
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Loans);
                view.Filter = FilterLoans;
                return view;
            }
        }

        partial void OnSearchQueryChanged(string value) => LoansView.Refresh();
        partial void OnBranchFilterChanged(string value) => LoansView.Refresh();
        partial void OnStatusFilterChanged(string value) => LoansView.Refresh();
        partial void OnMinPrincipalChanged(decimal? value) => LoansView.Refresh();
        partial void OnMaxPrincipalChanged(decimal? value) => LoansView.Refresh();
        partial void OnMinInstallmentChanged(decimal? value) => LoansView.Refresh();
        partial void OnMaxInstallmentChanged(decimal? value) => LoansView.Refresh();
        partial void OnMinBalanceChanged(decimal? value) => LoansView.Refresh();
        partial void OnMaxBalanceChanged(decimal? value) => LoansView.Refresh();
        partial void OnMinInterestRateChanged(decimal? value) => LoansView.Refresh();
        partial void OnMaxInterestRateChanged(decimal? value) => LoansView.Refresh();
        partial void OnStartDateFromChanged(DateTime? value) => LoansView.Refresh();
        partial void OnStartDateToChanged(DateTime? value) => LoansView.Refresh();

        private bool FilterLoans(object obj)
        {
            if (obj is not EmployeeLoan loan) return false;

            // Search query (Employee Name)
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                var name = loan.Employee?.DisplayName ?? string.Empty;
                if (!name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Branch filter
            if (BranchFilter != "All" && !string.IsNullOrWhiteSpace(BranchFilter))
            {
                var branch = loan.Employee?.Branch.ToString() ?? string.Empty;
                if (!branch.Equals(BranchFilter, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // Status filter
            if (StatusFilter != "All" && !string.IsNullOrWhiteSpace(StatusFilter))
            {
                bool expectedActive = StatusFilter.Equals("Active", StringComparison.OrdinalIgnoreCase);
                if (loan.IsActive != expectedActive)
                    return false;
            }

            // Principal
            if (MinPrincipal.HasValue && loan.PrincipalAmount < MinPrincipal.Value) return false;
            if (MaxPrincipal.HasValue && loan.PrincipalAmount > MaxPrincipal.Value) return false;

            // Balance
            if (MinBalance.HasValue && loan.OutstandingBalance < MinBalance.Value) return false;
            if (MaxBalance.HasValue && loan.OutstandingBalance > MaxBalance.Value) return false;

            // Installment
            if (MinInstallment.HasValue && loan.MonthlyInstallment < MinInstallment.Value) return false;
            if (MaxInstallment.HasValue && loan.MonthlyInstallment > MaxInstallment.Value) return false;

            // Interest Rate
            if (MinInterestRate.HasValue)
            {
                decimal rateVal = loan.InterestRate;
                if (rateVal < 1m && MinInterestRate.Value > 1m) rateVal = rateVal * 100m;
                if (rateVal < MinInterestRate.Value) return false;
            }
            if (MaxInterestRate.HasValue)
            {
                decimal rateVal = loan.InterestRate;
                if (rateVal < 1m && MaxInterestRate.Value > 1m) rateVal = rateVal * 100m;
                if (rateVal > MaxInterestRate.Value) return false;
            }

            // Start Date
            if (StartDateFrom.HasValue && loan.StartDate < StartDateFrom.Value) return false;
            if (StartDateTo.HasValue && loan.StartDate > StartDateTo.Value) return false;

            return true;
        }

        [RelayCommand]
        public void ToggleAdvancedFilters() => IsAdvancedFiltersVisible = !IsAdvancedFiltersVisible;

        [RelayCommand]
        public void ClearFilters()
        {
            SearchQuery = string.Empty;
            BranchFilter = "All";
            StatusFilter = "All";
            MinPrincipal = null;
            MaxPrincipal = null;
            MinInstallment = null;
            MaxInstallment = null;
            MinBalance = null;
            MaxBalance = null;
            MinInterestRate = null;
            MaxInterestRate = null;
            StartDateFrom = null;
            StartDateTo = null;
            LoansView.Refresh();
        }

        [RelayCommand]
        public async Task PrintFilteredLoansAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Generating Report PDF...";
                
                var filteredItems = LoansView.Cast<EmployeeLoan>().ToList();
                
                var cols = new List<ReportColumnDefinition>
                {
                    new() { Header = "Employee", PropertyName = "Employee", Width = 2.0 },
                    new() { Header = "Branch", PropertyName = "Branch", Width = 1.0 },
                    new() { Header = "Principal (R)", PropertyName = "Principal", Width = 1.0 },
                    new() { Header = "Installment (R)", PropertyName = "Installment", Width = 1.0 },
                    new() { Header = "Balance (R)", PropertyName = "Balance", Width = 1.0 },
                    new() { Header = "Interest (%)", PropertyName = "InterestPercent", Width = 0.8 },
                    new() { Header = "Start Date", PropertyName = "StartDate", Width = 1.2 },
                    new() { Header = "Status", PropertyName = "Status", Width = 0.8 }
                };

                var printItems = filteredItems.Select(l => new LoanPrintModel
                {
                    Employee = l.Employee?.DisplayName ?? "Unknown",
                    Branch = l.Employee?.Branch.ToString() ?? "Unknown",
                    Principal = l.PrincipalAmount,
                    Installment = l.MonthlyInstallment,
                    Balance = l.OutstandingBalance,
                    InterestPercent = (double)(l.InterestRate < 1m && l.InterestRate > 0m ? l.InterestRate * 100m : l.InterestRate),
                    StartDate = l.StartDate,
                    Status = l.IsActive ? "ACTIVE" : "PAID"
                }).ToList();

                var path = await _pdfService.GenerateListReportPdfAsync("Staff Loans Report", printItems, cols);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing filtered loans");
                System.Windows.MessageBox.Show($"Failed to generate PDF:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }
    }

    public class LoanPrintModel
    {
        public string Employee { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public decimal Principal { get; set; }
        public decimal Installment { get; set; }
        public decimal Balance { get; set; }
        public double InterestPercent { get; set; }
        public DateTime StartDate { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
