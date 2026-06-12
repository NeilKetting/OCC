using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
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

        // Computed repayment preview
        public decimal TotalRepayableAmount => CalculateTotalRepayable();
        public string RepaymentDurationText
        {
            get
            {
                if (PrincipalAmount <= 0 || MonthlyInstallment <= 0) return "-";
                var total = TotalRepayableAmount;
                if (total == 0) return "Indefinite (installment too low)";
                var payments = (double)(total / MonthlyInstallment);
                return SelectedEmployee?.RateType == OCC.Shared.Models.RateType.Hourly
                    ? $"{payments:N1} Fortnights"
                    : $"{payments:N1} Months";
            }
        }

        partial void OnPrincipalAmountChanged(decimal value)     { OnPropertyChanged(nameof(TotalRepayableAmount)); OnPropertyChanged(nameof(RepaymentDurationText)); }
        partial void OnMonthlyInstallmentChanged(decimal value)  { OnPropertyChanged(nameof(TotalRepayableAmount)); OnPropertyChanged(nameof(RepaymentDurationText)); }
        partial void OnInterestRateChanged(decimal value)        { OnPropertyChanged(nameof(TotalRepayableAmount)); OnPropertyChanged(nameof(RepaymentDurationText)); }
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
                    employees.OrderBy(e => e.LastName));
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
                    OutstandingBalance = PrincipalAmount,
                    MonthlyInstallment = MonthlyInstallment,
                    InterestRate       = InterestRate,
                    StartDate          = LoanStartDate,
                    IsActive           = true,
                    Notes              = LoanNotes
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
                BusyText = "Generating PDF...";
                var path = await _pdfService.GenerateLoanSchedulePdfAsync(loan, loan.Employee);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error printing loan schedule");
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
            if (Employees.Any()) SelectedEmployee = Employees.First();
        }

        /// <summary>
        /// Amortized total repayable amount using monthly compounding.
        /// Formula: n = -log(1 - (r × P) / I) / log(1 + r);  Total = n × I
        /// </summary>
        private decimal CalculateTotalRepayable()
        {
            if (MonthlyInstallment <= 0 || PrincipalAmount <= 0) return 0;
            if (InterestRate <= 0) return PrincipalAmount;

            double r = (double)InterestRate / 100.0 / 12.0;
            double p = (double)PrincipalAmount;
            double i = (double)MonthlyInstallment;
            if (i <= p * r) return 0;

            double n = -Math.Log(1 - (r * p) / i) / Math.Log(1 + r);
            return (decimal)(n * i);
        }
    }
}
