using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    /// <summary>
    /// Wage Run ViewModel — generates fortnightly draft, allows in-grid edits, then finalizes.
    /// Ported from OCC.Client WageRunViewModel, adapted to WPF DI infrastructure.
    /// </summary>
    public partial class WageRunViewModel : ViewModelBase
    {
        private readonly IWageService _wageService;
        private readonly IPdfService _pdfService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<WageRunViewModel> _logger;

        private WageRun? _currentDraft;
        private Guid? _currentDraftId;

        // ─── Date Range ───────────────────────────────────────────────────────

        [ObservableProperty] private DateTime _startDate;
        [ObservableProperty] private DateTime _endDate;

        partial void OnStartDateChanged(DateTime value)
        {
            // Fortnight cycle: StartDate + 13 days = 14-day run
            EndDate = value.AddDays(13);
        }

        // ─── Run Config ───────────────────────────────────────────────────────

        [ObservableProperty] private string _selectedPayType = "Hourly";
        [ObservableProperty] private string _selectedBranch = "All";
        [ObservableProperty] private decimal _totalGasCharge = 0m;
        [ObservableProperty] private decimal _defaultSupervisorFee = 500m;
        [ObservableProperty] private decimal _companyHousingWashingFee = 0m;
        [ObservableProperty] private string _notes = string.Empty;
        [ObservableProperty] private bool _isSalaryVersion;

        public ObservableCollection<string> PayTypeOptions { get; } = new()
        {
            "Hourly",
            "MonthlySalary"
        };

        public ObservableCollection<string> BranchOptions { get; } = new()
        {
            "All",
            "Johannesburg",
            "Cape Town"
        };

        // ─── Live update: redistribute gas/washing/supervisor fee when inputs change

        partial void OnTotalGasChargeChanged(decimal value)
        {
            if (!Lines.Any()) return;
            var housedCount = Lines.Count(x => x.Model.IsCompanyHoused);
            var gasPerPerson = housedCount > 0 ? value / housedCount : 0;
            foreach (var line in Lines.Where(x => x.Model.IsCompanyHoused))
                line.DeductionGas = gasPerPerson;
        }

        partial void OnCompanyHousingWashingFeeChanged(decimal value)
        {
            if (!Lines.Any()) return;
            foreach (var line in Lines.Where(x => x.Model.IsCompanyHoused))
                line.DeductionWashing = value;
        }

        partial void OnDefaultSupervisorFeeChanged(decimal value)
        {
            if (!Lines.Any()) return;
            foreach (var line in Lines.Where(x => x.Model.IsSupervisor))
                line.IncentiveSupervisor = value;
        }

        // ─── Grid data ────────────────────────────────────────────────────────

        [ObservableProperty] private ObservableCollection<WageRunLineViewModel> _lines = new();
        [ObservableProperty] private decimal _grandTotalWage;
        [ObservableProperty] private bool _isGenerated;

        // Column Visibility
        [ObservableProperty] private bool _isIndexVisible = true;
        [ObservableProperty] private bool _isBasVisible = true;
        [ObservableProperty] private bool _isNameVisible = true;
        [ObservableProperty] private bool _isRateHrVisible = true;
        [ObservableProperty] private bool _isHrsVisible = true;
        [ObservableProperty] private bool _isOtRatesVisible = true;
        [ObservableProperty] private bool _isDeductionsVisible = true;
        [ObservableProperty] private bool _isSupFeeVisible = true;
        [ObservableProperty] private bool _isTotalNettVisible = true;
        [ObservableProperty] private bool _isTotalRemVisible = true;
        [ObservableProperty] private bool _isDaysVisible = true;
        [ObservableProperty] private bool _isNotesVisible = true;

        // ─── Past runs list ───────────────────────────────────────────────────

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredPastRuns))]
        private string _selectedPastBranch = "All";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredPastRuns))]
        private string _selectedPastSalaryType = "All";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredPastRuns))]
        private ObservableCollection<WageRun> _pastRuns = new();

        [ObservableProperty] private WageRun? _selectedPastRun;

        public ObservableCollection<string> PayTypeOptionsWithAll { get; } = new()
        {
            "All",
            "Hourly",
            "MonthlySalary"
        };

        public IEnumerable<WageRun> FilteredPastRuns
        {
            get
            {
                var filtered = PastRuns.AsEnumerable();
                if (SelectedPastBranch != "All")
                {
                    filtered = filtered.Where(r => r.Branch == SelectedPastBranch);
                }
                if (SelectedPastSalaryType != "All")
                {
                    filtered = filtered.Where(r => r.PayType == SelectedPastSalaryType ||
                        (SelectedPastSalaryType == "MonthlySalary" && r.PayType == "MonthlySalary"));
                }
                return filtered.ToList();
            }
        }

        // ─── Constructor ─────────────────────────────────────────────────────

        public WageRunViewModel(
            IWageService wageService,
            IPdfService pdfService,
            IDialogService dialogService,
            ILogger<WageRunViewModel> logger)
        {
            _wageService = wageService;
            _pdfService = pdfService;
            _dialogService = dialogService;
            _logger = logger;
            Title = "Wage Run";

            // Default: Monday of previous fortnight (runs are typically generated on Wednesday of Week 2)
            var today = DateTime.Today;
            int diff = (7 + (int)(today.DayOfWeek - DayOfWeek.Monday)) % 7;
            StartDate = today.AddDays(-diff).AddDays(-7).Date;
        }

        // ─── Commands ────────────────────────────────────────────────────────

        [RelayCommand]
        public async Task LoadDataAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Loading past runs...";
                var runs = await _wageService.GetWageRunsAsync();
                PastRuns = new ObservableCollection<WageRun>(runs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading past wage runs");
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task GenerateDraftAsync()
        {
            try
            {
                IsBusy = true;
                BusyText = "Generating draft run...";

                // Preserve manual edits before regenerating
                var existingEdits = new Dictionary<Guid, (decimal Washing, decimal SupFee)>();
                if (IsGenerated && Lines.Any())
                {
                    foreach (var line in Lines)
                        existingEdits[line.Model.EmployeeId] = (line.DeductionWashing, line.IncentiveSupervisor);
                }

                _currentDraft = await _wageService.GenerateDraftRunAsync(
                    StartDate, EndDate, SelectedPayType, SelectedBranch,
                    TotalGasCharge, DefaultSupervisorFee, CompanyHousingWashingFee, Notes);
                _currentDraftId = _currentDraft.Id;

                Lines.Clear();
                int index = 1;

                // Consolidate duplicate employee lines (multi-branch edge case)
                var consolidated = _currentDraft.Lines
                    .GroupBy(l => new { Name = l.EmployeeName?.Trim(), l.EmployeeId })
                    .Select(g =>
                    {
                        var first = g.First();
                        foreach (var extra in g.Skip(1))
                        {
                            first.TotalWage           += extra.TotalWage;
                            first.NormalHours         += extra.NormalHours;
                            first.Overtime15Hours     += extra.Overtime15Hours;
                            first.Overtime20Hours     += extra.Overtime20Hours;
                            first.IncentiveSupervisor += extra.IncentiveSupervisor;
                            first.DeductionLoan       += extra.DeductionLoan;
                            first.DeductionWashing    += extra.DeductionWashing;
                            first.DeductionGas        += extra.DeductionGas;
                            first.DeductionOther      += extra.DeductionOther;
                            first.DeductionPPE        += extra.DeductionPPE;
                            if (!string.IsNullOrEmpty(extra.VarianceNotes))
                                first.VarianceNotes = (first.VarianceNotes + " " + extra.VarianceNotes).Trim();
                        }
                        return first;
                    })
                    .OrderBy(l => l.EmployeeName);

                foreach (var line in consolidated)
                {
                    // Re-apply previous manual edits
                    if (existingEdits.TryGetValue(line.EmployeeId, out var edits))
                    {
                        line.DeductionWashing    = edits.Washing;
                        line.IncentiveSupervisor = edits.SupFee;
                    }

                    var vm = new WageRunLineViewModel(line, _dialogService) { IndexNum = index++ };
                    vm.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(WageRunLineViewModel.NetPay) ||
                            e.PropertyName == nameof(WageRunLineViewModel.IncentiveSupervisor))
                            UpdateGrandTotal();
                    };
                    Lines.Add(vm);
                }

                UpdateGrandTotal();
                IsGenerated = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating draft wage run");
                System.Windows.MessageBox.Show(
                    $"Failed to generate draft:\n\n{ex.Message}",
                    "Wage Run Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task FinalizeRunAsync()
        {
            if (_currentDraftId == null || _currentDraft == null) return;

            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to finalize this wage run?\n\nThis will lock attendance variances for future runs.",
                "Finalize Wage Run", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                IsBusy = true;
                BusyText = "Finalizing run...";

                _currentDraft.Lines = Lines.Select(vm => vm.Model).ToList();
                _currentDraft.Notes = Notes;

                var finalized = await _wageService.FinalizeRunAsync(_currentDraft);

                System.Windows.MessageBox.Show(
                    $"Wage Run finalized successfully.\nRun ID: {finalized.Id}",
                    "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                Lines.Clear();
                IsGenerated = false;
                _currentDraft = null;
                _currentDraftId = null;

                // Refresh history list
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalizing wage run");
                System.Windows.MessageBox.Show(
                    $"Failed to finalize run:\n\n{ex.Message}",
                    "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task PrintPdfAsync()
        {
            if (_currentDraft == null && !Lines.Any()) return;

            try
            {
                IsBusy = true;
                BusyText = "Generating PDF...";

                var runToPrint = _currentDraft ?? new WageRun
                {
                    StartDate = StartDate,
                    EndDate   = EndDate,
                    Branch    = SelectedBranch,
                    PayType   = SelectedPayType,
                };
                runToPrint.Lines = Lines.Select(l => l.Model).ToList();

                var path = await _pdfService.GenerateWageRunPdfAsync(runToPrint, IsSalaryVersion);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating wage run PDF");
                System.Windows.MessageBox.Show($"Failed to generate PDF:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task PrintSupervisorPaymentsAsync()
        {
            if (_currentDraft == null && !Lines.Any()) return;

            try
            {
                IsBusy = true;
                BusyText = "Generating Supervisor Payments PDF...";

                var runToPrint = _currentDraft ?? new WageRun
                {
                    StartDate = StartDate,
                    EndDate   = EndDate,
                    Branch    = SelectedBranch,
                    PayType   = SelectedPayType,
                };
                runToPrint.Lines = Lines.Select(l => l.Model).ToList();

                var path = await _pdfService.GenerateSupervisorChecklistPdfAsync(runToPrint);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating supervisor payments PDF");
                System.Windows.MessageBox.Show($"Failed to generate supervisor payments PDF:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task DeletePastRunAsync(WageRun? run)
        {
            if (run == null) return;

            var result = System.Windows.MessageBox.Show(
                $"Delete wage run for {run.Branch} ({run.StartDate:dd MMM} – {run.EndDate:dd MMM yyyy})?",
                "Delete Run", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.Yes) return;

            try
            {
                await _wageService.DeleteRunAsync(run.Id);
                PastRuns.Remove(run);
                OnPropertyChanged(nameof(FilteredPastRuns));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting wage run {Id}", run.Id);
                System.Windows.MessageBox.Show($"Failed to delete run:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        private void UpdateGrandTotal()
            => GrandTotalWage = Lines.Sum(x => x.NetPay);
    }
}
