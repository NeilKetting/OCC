using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.IO;
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
        private readonly IExportService _exportService;
        private readonly IDialogService _dialogService;
        private readonly ILogger<WageRunViewModel> _logger;
        private readonly OCC.WpfClient.Services.Infrastructure.LocalSettingsService _localSettings;
        private readonly IPermissionService _permissionService;
        private bool _isInitializingColumns;

        private WageRun? _currentDraft;
        private Guid? _currentDraftId;

        // ─── Date Range ───────────────────────────────────────────────────────

        [ObservableProperty] private DateTime _startDate;
        [ObservableProperty] private DateTime _endDate;
        private bool _isUpdatingDates;
        private bool _isLoadingRun;

        public bool IsEditingPastRun => _currentDraft != null && (_currentDraft.Status == WageRunStatus.Finalized || _currentDraft.Status == WageRunStatus.Paid);
        public string FinalizeButtonText => IsEditingPastRun ? "SAVE CHANGES" : "FINALISE RUN";
        public string FinalizeButtonIcon => IsEditingPastRun ? "\uE74E" : "\uE930";

        partial void OnStartDateChanged(DateTime value)
        {
            if (_isUpdatingDates) return;
            _isUpdatingDates = true;
            try
            {
                if (SelectedPayFrequency == PayFrequency.Monthly || string.Equals(SelectedPayType, "MonthlySalary", StringComparison.OrdinalIgnoreCase))
                {
                    var firstDay = new DateTime(value.Year, value.Month, 1);
                    var lastDay = new DateTime(value.Year, value.Month, DateTime.DaysInMonth(value.Year, value.Month));
                    if (StartDate != firstDay) StartDate = firstDay;
                    EndDate = lastDay;
                }
                else if (SelectedPayFrequency == PayFrequency.Weekly)
                {
                    EndDate = value.AddDays(6);
                }
                else // Fortnightly
                {
                    EndDate = value.AddDays(13);
                }
                IsDecColumnsVisible = value.Month == 12 || value.Month == 1;
            }
            finally
            {
                _isUpdatingDates = false;
            }
        }

        partial void OnEndDateChanged(DateTime value)
        {
            if (_isUpdatingDates) return;
            _isUpdatingDates = true;
            try
            {
                if (SelectedPayFrequency == PayFrequency.Monthly || string.Equals(SelectedPayType, "MonthlySalary", StringComparison.OrdinalIgnoreCase))
                {
                    var firstDay = new DateTime(value.Year, value.Month, 1);
                    var lastDay = new DateTime(value.Year, value.Month, DateTime.DaysInMonth(value.Year, value.Month));
                    StartDate = firstDay;
                    if (EndDate != lastDay) EndDate = lastDay;
                }
                else if (SelectedPayFrequency == PayFrequency.Weekly)
                {
                    StartDate = value.AddDays(-6);
                }
                else // Fortnightly
                {
                    StartDate = value.AddDays(-13);
                }
                IsDecColumnsVisible = StartDate.Month == 12 || StartDate.Month == 1;
            }
            finally
            {
                _isUpdatingDates = false;
            }
        }

        // ─── Run Config ───────────────────────────────────────────────────────

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredPastRuns))]
        private string _selectedPayType = "Hourly";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredPastRuns))]
        private string _selectedBranch = "All";

        [ObservableProperty] private decimal _totalGasCharge = 0m;
        [ObservableProperty] private decimal _defaultSupervisorFee = 500m;
        [ObservableProperty] private decimal _companyHousingWashingFee = 0m;
        [ObservableProperty] private string _notes = string.Empty;
        [ObservableProperty] private bool _isSalaryVersion;
        [ObservableProperty] private bool _excludeZeroWages;

        public bool IsExcludeZeroWagesToggleVisible => true;

        partial void OnExcludeZeroWagesChanged(bool value)
        {
            LinesView?.Refresh();
            UpdateGrandTotal();
        }

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

        public ObservableCollection<WageRunType> RunTypeOptions { get; } = new()
        {
            WageRunType.Standard,
            WageRunType.AdHocAdvance,
            WageRunType.Correction
        };

        public ObservableCollection<PayFrequency> PayFrequencyOptions { get; } = new()
        {
            PayFrequency.Weekly,
            PayFrequency.Fortnightly,
            PayFrequency.Monthly
        };

        [ObservableProperty] private WageRunType _selectedRunType = WageRunType.Standard;
        [ObservableProperty] private PayFrequency _selectedPayFrequency = PayFrequency.Fortnightly;

        partial void OnSelectedPayFrequencyChanged(PayFrequency value)
        {
            OnPropertyChanged(nameof(IsWeek2ColumnVisible));
            UpdateDatesFromPreviousRun();
        }

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
        [ObservableProperty] private bool _isOtHoursVisible = true;
        [ObservableProperty] private bool _isDecColumnsVisible;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsWashingGasVisible))]
        private bool _isDeductionsVisible = true;
        [ObservableProperty] private bool _isSupFeeVisible = true;
        [ObservableProperty] private bool _isTotalNettVisible = true;
        [ObservableProperty] private bool _isTotalRemVisible = true;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsWeek2ColumnVisible))]
        private bool _isDaysVisible = true;

        public bool IsWeek2ColumnVisible => IsDaysVisible && SelectedPayFrequency != PayFrequency.Weekly;
        [ObservableProperty] private bool _isNotesVisible = true;
        [ObservableProperty] private bool _isBankVisible = true;
        [ObservableProperty] private bool _isBankAccountVisible = true;
        [ObservableProperty] private bool _isCommentsVisible = true;

        [ObservableProperty] private string _searchQuery = string.Empty;

        public System.ComponentModel.ICollectionView LinesView
        {
            get
            {
                var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Lines);
                if (view.Filter != FilterLines)
                {
                    view.Filter = FilterLines;
                }
                return view;
            }
        }

        private bool FilterLines(object obj)
        {
            if (obj is not WageRunLineViewModel line) return false;

            // Extra safety permission check inside FilterLines
            bool hasJhb = _permissionService.CanAccess(NavigationRoutes.Feature_WagesJhb);
            bool hasCpt = _permissionService.CanAccess(NavigationRoutes.Feature_WagesCpt);
            if (!hasJhb || !hasCpt)
            {
                if (hasJhb && !(string.Equals(line.Branch, "Johannesburg", StringComparison.OrdinalIgnoreCase) || string.Equals(line.Branch, "JHB", StringComparison.OrdinalIgnoreCase)))
                    return false;
                if (hasCpt && !(string.Equals(line.Branch, "Cape Town", StringComparison.OrdinalIgnoreCase) || string.Equals(line.Branch, "CPT", StringComparison.OrdinalIgnoreCase)))
                    return false;
                if (!hasJhb && !hasCpt)
                    return false;
            }

            // Client-side PayType filter matching UI selection
            if (_currentDraft != null && !string.IsNullOrEmpty(_currentDraft.PayType) &&
                !string.Equals(_currentDraft.PayType, SelectedPayType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Client-side branch filter matching UI selection
            if (SelectedBranch != "All" && !string.Equals(line.Branch, SelectedBranch, StringComparison.OrdinalIgnoreCase))
                return false;

            // Exclude zero wages if toggle is active
            if (ExcludeZeroWages)
            {
                if (line.TotalRem == 0 && line.NetPay == 0)
                    return false;
            }

            // Client-side search filter
            if (string.IsNullOrWhiteSpace(SearchQuery)) return true;

            var q = SearchQuery.Trim();
            return (line.EmployeeName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false) ||
                   (line.EmployeeNumber?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        partial void OnSearchQueryChanged(string value)
        {
            LinesView.Refresh();
            UpdateGrandTotal();
        }
        public bool IsBibcColumnVisible => SelectedBranch.ToBranchEnum() == Branch.CPT;
        public bool IsBibcRateVisible => IsAdminUser && SelectedBranch.ToBranchEnum() == Branch.CPT;
        public bool IsWashingGasVisible => IsDeductionsVisible && SelectedBranch.ToBranchEnum() != Branch.CPT;
        public bool IsWeekBreakdownVisible => SelectedBranch.ToBranchEnum() != Branch.CPT;

        private void UpdateDatesForSelectedBranch()
        {
            if (_isUpdatingDates) return;
            _isUpdatingDates = true;
            try
            {
                if (string.Equals(SelectedPayType, "MonthlySalary", StringComparison.OrdinalIgnoreCase))
                {
                    var baseDate = StartDate != default ? StartDate : DateTime.Today;
                    StartDate = new DateTime(baseDate.Year, baseDate.Month, 1);
                    EndDate = new DateTime(baseDate.Year, baseDate.Month, DateTime.DaysInMonth(baseDate.Year, baseDate.Month));
                    IsDecColumnsVisible = StartDate.Month == 12 || StartDate.Month == 1;
                    return;
                }

                var branchName = SelectedBranch;
                var payType = SelectedPayType;
                var pastRun = PastRuns
                    .Where(r => (r.Status == WageRunStatus.Finalized || r.Status == WageRunStatus.Paid) &&
                                (string.IsNullOrEmpty(r.PayType) || string.Equals(r.PayType, payType, StringComparison.OrdinalIgnoreCase)) &&
                                (branchName == "All" ||
                                 string.Equals(r.Branch, branchName, StringComparison.OrdinalIgnoreCase) ||
                                 (string.Equals(branchName, "Johannesburg", StringComparison.OrdinalIgnoreCase) && string.Equals(r.Branch, "JHB", StringComparison.OrdinalIgnoreCase)) ||
                                 (string.Equals(branchName, "Cape Town", StringComparison.OrdinalIgnoreCase) && string.Equals(r.Branch, "CPT", StringComparison.OrdinalIgnoreCase))))
                    .OrderByDescending(r => r.EndDate)
                    .FirstOrDefault();

                DateTime newStart;
                bool isCpt = SelectedBranch.ToBranchEnum() == Branch.CPT;
                int days = isCpt ? 6 : 13;

                if (pastRun != null)
                {
                    newStart = pastRun.EndDate.AddDays(1).Date;
                }
                else
                {
                    var today = DateTime.Today;
                    int diff = (7 + (int)(today.DayOfWeek - DayOfWeek.Saturday)) % 7;
                    newStart = today.AddDays(-diff).AddDays(-7).Date;
                }

                StartDate = newStart;
                EndDate = newStart.AddDays(days);
                IsDecColumnsVisible = StartDate.Month == 12 || StartDate.Month == 1;
            }
            finally
            {
                _isUpdatingDates = false;
            }
        }

        partial void OnSelectedBranchChanged(string value)
        {
            LinesView?.Refresh();
            UpdateGrandTotal();

            UpdateDatesFromPreviousRun();

            var branchEnum = value.ToBranchEnum();
            IsDeductionsButtonVisible = branchEnum != Branch.CPT;
            IsDeductionsVisible = true;
            OnPropertyChanged(nameof(IsBibcColumnVisible));
            OnPropertyChanged(nameof(IsBibcRateVisible));
            OnPropertyChanged(nameof(IsWashingGasVisible));
            OnPropertyChanged(nameof(IsWeekBreakdownVisible));
        }
        partial void OnSelectedPayTypeChanged(string value)
        {
            OnPropertyChanged(nameof(IsExcludeZeroWagesToggleVisible));
            UpdateDatesFromPreviousRun();
            LinesView?.Refresh();
            UpdateGrandTotal();
        }

        // ─── Past runs list ───────────────────────────────────────────────────

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(FilteredPastRuns))]
        private ObservableCollection<WageRun> _pastRuns = new();

        [ObservableProperty] private WageRun? _selectedPastRun;

        public IEnumerable<WageRun> FilteredPastRuns
        {
            get
            {
                var filtered = PastRuns.AsEnumerable();
                
                bool hasJhb = _permissionService.CanAccess(NavigationRoutes.Feature_WagesJhb);
                bool hasCpt = _permissionService.CanAccess(NavigationRoutes.Feature_WagesCpt);

                if (hasJhb && !hasCpt)
                {
                    filtered = filtered.Where(r => r.Branch.IsJohannesburg());
                }
                else if (hasCpt && !hasJhb)
                {
                    filtered = filtered.Where(r => r.Branch.IsCapeTown());
                }
                else if (SelectedBranch != "All")
                {
                    filtered = filtered.Where(r => r.Branch.MatchesBranch(SelectedBranch));
                }

                if (!string.IsNullOrEmpty(SelectedPayType))
                {
                    filtered = filtered.Where(r => r.PayType == SelectedPayType);
                }
                return filtered.OrderByDescending(r => r.StartDate).ToList();
            }
        }

        // ─── Constructor ─────────────────────────────────────────────────────

        private readonly ISettingsService _settingsService;
        private readonly IAuthService _authService;

        [ObservableProperty] private decimal _bibcRate = 28.75m;
        [ObservableProperty] private bool _isAdminUser;
        [ObservableProperty] private bool _isDeductionsButtonVisible = true;

        private async Task LoadBibcRateAsync()
        {
            try
            {
                BibcRate = await _settingsService.GetBibcRateAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading BIBC rate");
            }
        }

        partial void OnBibcRateChanged(decimal value)
        {
            if (IsAdminUser)
            {
                _ = SaveBibcRateAsync(value);
            }
        }

        private async Task SaveBibcRateAsync(decimal value)
        {
            try
            {
                await _settingsService.SaveBibcRateAsync(value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving BIBC rate");
            }
        }

        private readonly ISignalRService _signalRService;

        public WageRunViewModel(
            IWageService wageService,
            IPdfService pdfService,
            IExportService exportService,
            IDialogService dialogService,
            ILogger<WageRunViewModel> logger,
            OCC.WpfClient.Services.Infrastructure.LocalSettingsService localSettings,
            IPermissionService permissionService,
            ISettingsService settingsService,
            IAuthService authService,
            ISignalRService signalRService)
        {
            _wageService = wageService;
            _pdfService = pdfService;
            _exportService = exportService;
            _dialogService = dialogService;
            _logger = logger;
            _localSettings = localSettings;
            _permissionService = permissionService;
            _settingsService = settingsService;
            _authService = authService;
            _signalRService = signalRService;
            Title = "Wage Run";

            _signalRService.OnWageRunChanged += (change) =>
            {
                if (change?.Entity != null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        var run = change.Entity;
                        var existing = PastRuns.FirstOrDefault(r => r.Id == run.Id);
                        if (existing != null)
                        {
                            var idx = PastRuns.IndexOf(existing);
                            PastRuns[idx] = run;
                        }
                        else
                        {
                            PastRuns.Insert(0, run);
                        }
                        OnPropertyChanged(nameof(FilteredPastRuns));
                    });
                }
            };

            _signalRService.OnWageSettingsChanged += (change) =>
            {
                if (change?.Entity != null)
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        DefaultSupervisorFee = change.Entity.DefaultSupervisorFee;
                        CompanyHousingWashingFee = change.Entity.DefaultCompanyHousingWashingFee;
                    });
                }
            };

            IsAdminUser = authService.CurrentUser?.UserRole == UserRole.Admin;
            _ = LoadWageSettingsDefaultsAsync();

            // Default: Saturday of previous fortnight (runs are typically generated on Wednesday of Week 2)
            var today = DateTime.Today;
            int diff = (7 + (int)(today.DayOfWeek - DayOfWeek.Saturday)) % 7;
            StartDate = today.AddDays(-diff).AddDays(-7).Date;
            IsDecColumnsVisible = StartDate.Month == 12 || StartDate.Month == 1;

            // Dynamically set branch options based on permissions
            BranchOptions.Clear();
            bool hasJhb = permissionService.CanAccess(NavigationRoutes.Feature_WagesJhb);
            bool hasCpt = permissionService.CanAccess(NavigationRoutes.Feature_WagesCpt);

            if (hasJhb && hasCpt)
            {
                BranchOptions.Add("All");
                BranchOptions.Add("Johannesburg");
                BranchOptions.Add("Cape Town");
                _selectedBranch = "All";
            }
            else if (hasJhb)
            {
                BranchOptions.Add("Johannesburg");
                _selectedBranch = "Johannesburg";
            }
            else if (hasCpt)
            {
                BranchOptions.Add("Cape Town");
                _selectedBranch = "Cape Town";
            }
            else
            {
                BranchOptions.Add("All");
                BranchOptions.Add("Johannesburg");
                BranchOptions.Add("Cape Town");
                _selectedBranch = "All";
            }

            LoadColumnVisibilities();
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
                await LoadWageSettingsDefaultsAsync();
                UpdateDatesFromPreviousRun();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading past wage runs");
            }
            finally { IsBusy = false; }
        }

        private void UpdateDatesFromPreviousRun()
        {
            if (_isUpdatingDates || _isLoadingRun) return;

            var previousRun = PastRuns
                .Where(r => (r.Status == WageRunStatus.Finalized || r.Status == WageRunStatus.Paid) &&
                            (r.Branch == SelectedBranch || SelectedBranch == "All" ||
                             (SelectedBranch == "Johannesburg" && (r.Branch == "JHB" || r.Branch == "Johannesburg")) ||
                             (SelectedBranch == "Cape Town" && (r.Branch == "CPT" || r.Branch == "Cape Town"))) &&
                            (string.IsNullOrEmpty(SelectedPayType) || r.PayType == SelectedPayType))
                .OrderByDescending(r => r.EndDate)
                .FirstOrDefault();

            DateTime newStart;
            if (previousRun != null && previousRun.EndDate != DateTime.MinValue)
            {
                newStart = previousRun.EndDate.AddDays(1).Date;
            }
            else if (SelectedPayFrequency == PayFrequency.Monthly || string.Equals(SelectedPayType, "MonthlySalary", StringComparison.OrdinalIgnoreCase))
            {
                newStart = DateTime.Today;
            }
            else
            {
                var today = DateTime.Today;
                int diff = (7 + (int)(today.DayOfWeek - DayOfWeek.Saturday)) % 7;
                newStart = today.AddDays(-diff).AddDays(SelectedPayFrequency == PayFrequency.Fortnightly ? -14 : -7).Date;
            }

            CalculateDatesForStart(newStart);
        }

        private void CalculateDatesForStart(DateTime start)
        {
            _isUpdatingDates = true;
            try
            {
                StartDate = start.Date;

                if (SelectedPayFrequency == PayFrequency.Monthly || string.Equals(SelectedPayType, "MonthlySalary", StringComparison.OrdinalIgnoreCase))
                {
                    var firstDay = new DateTime(start.Year, start.Month, 1);
                    var lastDay = new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month));
                    if (StartDate != firstDay) StartDate = firstDay;
                    EndDate = lastDay;
                }
                else if (SelectedPayFrequency == PayFrequency.Weekly)
                {
                    EndDate = StartDate.AddDays(6);
                }
                else // Fortnightly
                {
                    EndDate = StartDate.AddDays(13);
                }

                IsDecColumnsVisible = StartDate.Month == 12 || StartDate.Month == 1;
            }
            finally
            {
                _isUpdatingDates = false;
            }
        }

        private async Task LoadWageSettingsDefaultsAsync()
        {
            try
            {
                var settings = await _wageService.GetWageSettingsAsync();
                if (settings != null)
                {
                    DefaultSupervisorFee = settings.DefaultSupervisorFee;
                    CompanyHousingWashingFee = settings.DefaultCompanyHousingWashingFee;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading wage settings defaults into WageRunViewModel");
            }
        }

        [RelayCommand]
        public async Task GenerateDraftAsync()
        {
            if (IsBusy) return;
            try
            {
                IsBusy = true;
                BusyText = "Generating draft run...";

                WageRunLineViewModel.BibcRate = BibcRate;

                // Preserve manual edits before regenerating
                var existingEdits = new Dictionary<Guid, (decimal Washing, decimal SupFee)>();
                if (IsGenerated && Lines.Any())
                {
                    foreach (var line in Lines)
                        existingEdits[line.Model.EmployeeId] = (line.DeductionWashing, line.IncentiveSupervisor);
                }

                _currentDraft = await _wageService.GenerateDraftRunAsync(
                    StartDate, EndDate, SelectedPayType, SelectedBranch,
                    TotalGasCharge, DefaultSupervisorFee, CompanyHousingWashingFee, Notes,
                    SelectedRunType, SelectedPayFrequency);
                _currentDraftId = _currentDraft.Id;

                Lines.Clear();
                int index = 1;

                // Consolidate duplicate employee lines safely without mutating inside lazy LINQ projections
                var groupedLines = _currentDraft.Lines
                    .GroupBy(l => new { Name = l.EmployeeName?.Trim(), l.EmployeeId })
                    .ToList();

                var consolidatedList = new List<WageRunLine>();
                foreach (var group in groupedLines)
                {
                    var first = group.First();
                    foreach (var extra in group.Skip(1))
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
                    consolidatedList.Add(first);
                }

                var sortedList = consolidatedList
                    .OrderByDescending(l => l.EmploymentType == "Permanent")
                    .ThenBy(l => l.EmployeeName)
                    .ToList();

                foreach (var line in sortedList)
                {
                    // Re-apply previous manual edits
                    if (existingEdits.TryGetValue(line.EmployeeId, out var edits))
                    {
                        line.DeductionWashing    = edits.Washing;
                        line.IncentiveSupervisor = edits.SupFee;
                    }

                    var vm = new WageRunLineViewModel(line, _dialogService) { IndexNum = index++ };
                    vm.Recalculate();
                    vm.PropertyChanged += (s, e) =>
                    {
                        if (string.IsNullOrEmpty(e.PropertyName) ||
                            e.PropertyName == nameof(WageRunLineViewModel.NetPay) ||
                            e.PropertyName == nameof(WageRunLineViewModel.TotalWage) ||
                            e.PropertyName == nameof(WageRunLineViewModel.IncentiveSupervisor))
                            UpdateGrandTotal();
                    };
                    Lines.Add(vm);
                }

                UpdateGrandTotal();
                IsGenerated = true;
                OnPropertyChanged(nameof(IsEditingPastRun));
                OnPropertyChanged(nameof(FinalizeButtonText));
                OnPropertyChanged(nameof(FinalizeButtonIcon));
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
        public async Task OverrideLineAsync(WageRunLineViewModel? line)
        {
            if (line == null) return;

            bool result = await _dialogService.ShowWageRunOverrideDialogAsync(line);
            if (result)
            {
                line.Recalculate();
                UpdateGrandTotal();
            }
        }

        [RelayCommand]
        public async Task FinalizeRunAsync()
        {
            if (_currentDraftId == null || _currentDraft == null) return;

            bool isEditing = IsEditingPastRun;
            string confirmTitle = isEditing ? "Save Changes to Wage Run" : "Finalize Wage Run";
            string confirmMessage = isEditing
                ? "Are you sure you want to save changes to this wage run?"
                : "Are you sure you want to finalize this wage run?\n\nThis will lock attendance variances for future runs. An automatic database backup will be performed before finalizing.";

            var confirmed = await _dialogService.ShowConfirmationAsync(confirmTitle, confirmMessage);
            if (!confirmed) return;

            try
            {
                IsBusy = true;
                BusyText = isEditing ? "Saving changes..." : "Finalizing run...";

                _currentDraft.Lines = Lines.Select(vm => vm.Model).ToList();
                _currentDraft.Notes = Notes;

                var finalized = await _wageService.FinalizeRunAsync(_currentDraft);

                string successMsg = isEditing
                    ? $"Wage Run changes saved successfully.\nRun ID: {finalized.Id}"
                    : $"Wage Run finalized successfully.\nRun ID: {finalized.Id}";

                System.Windows.MessageBox.Show(
                    successMsg,
                    "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                Lines.Clear();
                IsGenerated = false;
                _currentDraft = null;
                _currentDraftId = null;

                OnPropertyChanged(nameof(IsEditingPastRun));
                OnPropertyChanged(nameof(FinalizeButtonText));
                OnPropertyChanged(nameof(FinalizeButtonIcon));

                // Refresh history list
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finalizing wage run");
                System.Windows.MessageBox.Show(
                    $"Failed to save run:\n\n{ex.Message}",
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

                var runToPrint = new WageRun
                {
                    StartDate = StartDate,
                    EndDate   = EndDate,
                    Branch    = SelectedBranch,
                    PayType   = SelectedPayType,
                    Notes     = Notes,
                    Lines     = LinesView.Cast<WageRunLineViewModel>().Select(l => l.Model).ToList()
                };

                var path = await _pdfService.GenerateWageRunPdfAsync(runToPrint, hideAfterComments: false, hideDecColumns: !IsDecColumnsVisible, visibleColumns: null);
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
        public async Task PrintSalaryVersionAsync()
        {
            if (_currentDraft == null && !Lines.Any()) return;

            try
            {
                IsBusy = true;
                BusyText = "Generating Salary Version PDF...";

                var runToPrint = new WageRun
                {
                    StartDate = StartDate,
                    EndDate   = EndDate,
                    Branch    = SelectedBranch,
                    PayType   = SelectedPayType,
                    Notes     = Notes,
                    Lines     = LinesView.Cast<WageRunLineViewModel>().Select(l => l.Model).ToList()
                };

                var path = await _pdfService.GenerateWageRunPdfAsync(runToPrint, hideAfterComments: true, hideDecColumns: !IsDecColumnsVisible, visibleColumns: null);
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating salary version PDF");
                System.Windows.MessageBox.Show($"Failed to generate Salary PDF:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task PrintFilteredPdfAsync()
        {
            if (_currentDraft == null && !Lines.Any()) return;

            try
            {
                IsBusy = true;
                BusyText = "Generating Filtered PDF...";

                var runToPrint = new WageRun
                {
                    StartDate = StartDate,
                    EndDate   = EndDate,
                    Branch    = SelectedBranch,
                    PayType   = SelectedPayType,
                    Notes     = Notes,
                    Lines     = LinesView.Cast<WageRunLineViewModel>().Select(l => l.Model).ToList()
                };

                var path = await _pdfService.GenerateWageRunPdfAsync(runToPrint, hideAfterComments: false, hideDecColumns: !IsDecColumnsVisible, visibleColumns: GetVisibleColumns());
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating filtered wage run PDF");
                System.Windows.MessageBox.Show($"Failed to generate Filtered PDF:\n\n{ex.Message}", "Error",
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

                var runToPrint = new WageRun
                {
                    StartDate = StartDate,
                    EndDate   = EndDate,
                    Branch    = SelectedBranch,
                    PayType   = SelectedPayType,
                    Notes     = Notes,
                    Lines     = LinesView.Cast<WageRunLineViewModel>().Select(l => l.Model).ToList()
                };

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
        public async Task EditPastRunAsync(WageRun? run)
        {
            if (run == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Loading run for editing...";

                var fullRun = await _wageService.GetWageRunByIdAsync(run.Id);
                if (fullRun != null)
                {
                    _isLoadingRun = true;
                    _isUpdatingDates = true;
                    try
                    {
                        _currentDraft = fullRun;
                        _currentDraftId = fullRun.Id;

                        SelectedBranch = fullRun.Branch ?? "All";
                        SelectedPayType = fullRun.PayType ?? "Hourly";

                        StartDate = fullRun.StartDate.Date;
                        EndDate = fullRun.EndDate.Date;

                        Notes = fullRun.Notes ?? string.Empty;
                        TotalGasCharge = fullRun.InputTotalGasCharge;
                        DefaultSupervisorFee = fullRun.InputDefaultSupervisorFee;
                        CompanyHousingWashingFee = fullRun.InputCompanyHousingWashingFee;
                        IsDecColumnsVisible = StartDate.Month == 12 || StartDate.Month == 1;

                        Lines.Clear();
                        int index = 1;
                        foreach (var line in fullRun.Lines.OrderByDescending(l => l.EmploymentType == "Permanent").ThenBy(l => l.EmployeeName))
                        {
                            var vm = new WageRunLineViewModel(line, _dialogService) { IndexNum = index++ };
                            vm.PropertyChanged += (s, e) =>
                            {
                                if (string.IsNullOrEmpty(e.PropertyName) ||
                                    e.PropertyName == nameof(WageRunLineViewModel.NetPay) ||
                                    e.PropertyName == nameof(WageRunLineViewModel.TotalWage) ||
                                    e.PropertyName == nameof(WageRunLineViewModel.IncentiveSupervisor))
                                    UpdateGrandTotal();
                            };
                            Lines.Add(vm);
                        }

                        UpdateGrandTotal();
                        IsGenerated = true;

                        OnPropertyChanged(nameof(IsEditingPastRun));
                        OnPropertyChanged(nameof(FinalizeButtonText));
                        OnPropertyChanged(nameof(FinalizeButtonIcon));
                    }
                    finally
                    {
                        _isUpdatingDates = false;
                        _isLoadingRun = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading wage run for edit");
                System.Windows.MessageBox.Show($"Failed to load run:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task PrintPastRunPdfAsync(WageRun? run)
        {
            if (run == null) return;
            await PrintPastRunInternalAsync(run, hideAfterComments: false);
        }

        [RelayCommand]
        public async Task PrintPastRunSalaryAsync(WageRun? run)
        {
            if (run == null) return;
            await PrintPastRunInternalAsync(run, hideAfterComments: true);
        }

        [RelayCommand]
        public async Task PrintPastRunSupervisorPaymentsAsync(WageRun? run)
        {
            if (run == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Generating Supervisor Payments PDF...";

                var fullRun = await _wageService.GetWageRunByIdAsync(run.Id);
                if (fullRun != null)
                {
                    var path = await _pdfService.GenerateSupervisorChecklistPdfAsync(fullRun);
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating past supervisor payments PDF");
                System.Windows.MessageBox.Show($"Failed to generate supervisor payments PDF:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        private async Task PrintPastRunInternalAsync(WageRun run, bool hideAfterComments)
        {
            try
            {
                IsBusy = true;
                BusyText = "Generating PDF...";

                var fullRun = await _wageService.GetWageRunByIdAsync(run.Id);
                if (fullRun != null)
                {
                    var path = await _pdfService.GenerateWageRunPdfAsync(fullRun, hideAfterComments, hideDecColumns: !IsDecColumnsVisible, visibleColumns: null);
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating past wage run PDF");
                System.Windows.MessageBox.Show($"Failed to generate PDF:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task ExportPastRunBankFileAsync(WageRun? run)
        {
            if (run == null) return;

            try
            {
                IsBusy = true;
                BusyText = "Fetching payment details...";

                var payments = await _wageService.GetBankExportDataAsync(run.Id);
                var paymentList = payments.ToList();

                if (!paymentList.Any())
                {
                    System.Windows.MessageBox.Show("There are no valid employee payments (Net Pay > R0) in this wage run.", "No Payments",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var totalCount = paymentList.Count;
                var totalAmount = paymentList.Sum(p => p.Amount);

                IsBusy = false;

                var dialog = new Dialogs.BankExportDialogView(totalCount, totalAmount, DateTime.Today);
                if (dialog.ShowDialog() == true)
                {
                    var format = dialog.SelectedFormat;
                    var actionDate = dialog.ActionDate;

                    var defaultDocsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OCC", "BankExports");
                    if (!Directory.Exists(defaultDocsPath))
                    {
                        Directory.CreateDirectory(defaultDocsPath);
                    }

                    var cleanBranch = (run.Branch ?? "All").Replace(" ", "");
                    var defaultFilename = format == BankFormat.NedbankNetBankCsv
                        ? $"NedbankExport_{cleanBranch}_{run.EndDate:yyyyMMdd}.csv"
                        : $"BankExport_{cleanBranch}_{run.EndDate:yyyyMMdd}.csv";

                    var sfd = new Microsoft.Win32.SaveFileDialog
                    {
                        InitialDirectory = defaultDocsPath,
                        FileName = defaultFilename,
                        DefaultExt = ".csv",
                        Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                        Title = "Save Bank Export File"
                    };

                    if (sfd.ShowDialog() == true)
                    {
                        IsBusy = true;
                        BusyText = "Generating bank export file...";

                        await _exportService.GenerateBankExportFileAsync(paymentList, format, actionDate, sfd.FileName);

                        System.Windows.MessageBox.Show($"Bank export file generated successfully:\n\n{Path.GetFileName(sfd.FileName)}", "Success",
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                        var saveDir = Path.GetDirectoryName(sfd.FileName);
                        if (!string.IsNullOrEmpty(saveDir))
                        {
                            Process.Start(new ProcessStartInfo { FileName = saveDir, UseShellExecute = true });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting past bank file");
                System.Windows.MessageBox.Show($"Failed to export bank file:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task ExportDraftRunBankFileAsync()
        {
            if (_currentDraft == null && !Lines.Any()) return;

            try
            {
                IsBusy = true;
                BusyText = "Preparing draft payments...";

                var runToPreview = new WageRun
                {
                    Id = _currentDraftId ?? Guid.NewGuid(),
                    StartDate = StartDate,
                    EndDate = EndDate,
                    Branch = SelectedBranch,
                    PayType = SelectedPayType,
                    Notes = Notes,
                    Lines = LinesView.Cast<WageRunLineViewModel>().Select(l => l.Model).ToList()
                };

                var payments = await _wageService.GetBankExportPreviewAsync(runToPreview);
                var paymentList = payments.ToList();

                if (!paymentList.Any())
                {
                    System.Windows.MessageBox.Show("There are no valid employee payments (Net Pay > R0) in the current draft.", "No Payments",
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    return;
                }

                var totalCount = paymentList.Count;
                var totalAmount = paymentList.Sum(p => p.Amount);

                IsBusy = false;

                var dialog = new Dialogs.BankExportDialogView(totalCount, totalAmount, DateTime.Today);
                if (dialog.ShowDialog() == true)
                {
                    var format = dialog.SelectedFormat;
                    var actionDate = dialog.ActionDate;

                    var defaultDocsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "OCC", "BankExports");
                    if (!Directory.Exists(defaultDocsPath))
                    {
                        Directory.CreateDirectory(defaultDocsPath);
                    }

                    var cleanBranch = (SelectedBranch ?? "All").Replace(" ", "");
                    var defaultFilename = format == BankFormat.NedbankNetBankCsv
                        ? $"DraftNedbankExport_{cleanBranch}_{EndDate:yyyyMMdd}.csv"
                        : $"DraftBankExport_{cleanBranch}_{EndDate:yyyyMMdd}.csv";

                    var sfd = new Microsoft.Win32.SaveFileDialog
                    {
                        InitialDirectory = defaultDocsPath,
                        FileName = defaultFilename,
                        DefaultExt = ".csv",
                        Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*",
                        Title = "Save Draft Bank Export File"
                    };

                    if (sfd.ShowDialog() == true)
                    {
                        IsBusy = true;
                        BusyText = "Generating draft bank export file...";

                        await _exportService.GenerateBankExportFileAsync(paymentList, format, actionDate, sfd.FileName);

                        System.Windows.MessageBox.Show($"Draft bank export file generated successfully:\n\n{Path.GetFileName(sfd.FileName)}", "Success",
                            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

                        var saveDir = Path.GetDirectoryName(sfd.FileName);
                        if (!string.IsNullOrEmpty(saveDir))
                        {
                            Process.Start(new ProcessStartInfo { FileName = saveDir, UseShellExecute = true });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting draft bank file");
                System.Windows.MessageBox.Show($"Failed to export bank file:\n\n{ex.Message}", "Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        private void UpdateGrandTotal()
            => GrandTotalWage = Lines.Where(x => FilterLines(x)).Sum(x => x.NetPay);

        // ─── Column Selections Persistence ───────────────────────────────────

        private void LoadColumnVisibilities()
        {
            _isInitializingColumns = true;
            try
            {
                var cols = _localSettings.Settings.WageRunVisibleColumns;
                if (cols != null)
                {
                    if (cols.TryGetValue("Index", out bool index)) IsIndexVisible = index;
                    if (cols.TryGetValue("Bas", out bool bas)) IsBasVisible = bas;
                    if (cols.TryGetValue("Name", out bool name)) IsNameVisible = name;
                    if (cols.TryGetValue("RateHr", out bool rateHr)) IsRateHrVisible = rateHr;
                    if (cols.TryGetValue("Hrs", out bool hrs)) IsHrsVisible = hrs;
                    if (cols.TryGetValue("OtRates", out bool otRates)) IsOtRatesVisible = otRates;
                    if (cols.TryGetValue("OtHours", out bool otHours)) IsOtHoursVisible = otHours;
                    if (cols.TryGetValue("Deductions", out bool deductions)) IsDeductionsVisible = deductions;
                    if (cols.TryGetValue("SupFee", out bool supFee)) IsSupFeeVisible = supFee;
                    if (cols.TryGetValue("TotalNett", out bool totalNett)) IsTotalNettVisible = totalNett;
                    if (cols.TryGetValue("TotalRem", out bool totalRem)) IsTotalRemVisible = totalRem;
                    if (cols.TryGetValue("Days", out bool days)) IsDaysVisible = days;
                    if (cols.TryGetValue("Notes", out bool notes)) IsNotesVisible = notes;
                    if (cols.TryGetValue("Bank", out bool bank)) IsBankVisible = bank;
                    if (cols.TryGetValue("BankAccount", out bool bankAccount)) IsBankAccountVisible = bankAccount;
                    if (cols.TryGetValue("Comments", out bool comments)) IsCommentsVisible = comments;
                }
            }
            finally
            {
                _isInitializingColumns = false;
            }
        }

        private void SaveColumnVisibility(string columnName, bool isVisible)
        {
            if (_isInitializingColumns) return;

            if (_localSettings.Settings.WageRunVisibleColumns == null)
            {
                _localSettings.Settings.WageRunVisibleColumns = new Dictionary<string, bool>();
            }

            _localSettings.Settings.WageRunVisibleColumns[columnName] = isVisible;
            _localSettings.Save();
        }

        partial void OnIsIndexVisibleChanged(bool value) => SaveColumnVisibility("Index", value);
        partial void OnIsBasVisibleChanged(bool value) => SaveColumnVisibility("Bas", value);
        partial void OnIsNameVisibleChanged(bool value) => SaveColumnVisibility("Name", value);
        partial void OnIsRateHrVisibleChanged(bool value) => SaveColumnVisibility("RateHr", value);
        partial void OnIsHrsVisibleChanged(bool value) => SaveColumnVisibility("Hrs", value);
        partial void OnIsOtRatesVisibleChanged(bool value) => SaveColumnVisibility("OtRates", value);
        partial void OnIsOtHoursVisibleChanged(bool value) => SaveColumnVisibility("OtHours", value);
        partial void OnIsDeductionsVisibleChanged(bool value) => SaveColumnVisibility("Deductions", value);
        partial void OnIsSupFeeVisibleChanged(bool value) => SaveColumnVisibility("SupFee", value);
        partial void OnIsTotalNettVisibleChanged(bool value) => SaveColumnVisibility("TotalNett", value);
        partial void OnIsTotalRemVisibleChanged(bool value) => SaveColumnVisibility("TotalRem", value);
        partial void OnIsDaysVisibleChanged(bool value) => SaveColumnVisibility("Days", value);
        partial void OnIsNotesVisibleChanged(bool value) => SaveColumnVisibility("Notes", value);
        partial void OnIsBankVisibleChanged(bool value) => SaveColumnVisibility("Bank", value);
        partial void OnIsBankAccountVisibleChanged(bool value) => SaveColumnVisibility("BankAccount", value);
        partial void OnIsCommentsVisibleChanged(bool value) => SaveColumnVisibility("Comments", value);
        private Dictionary<string, bool> GetVisibleColumns()
        {
            return new Dictionary<string, bool>
            {
                { "Index", IsIndexVisible },
                { "Bas", IsBasVisible },
                { "Name", IsNameVisible },
                { "RateHr", IsRateHrVisible },
                { "Hrs", IsHrsVisible },
                { "OtRates", IsOtRatesVisible },
                { "OtHours", IsOtHoursVisible },
                { "DecColumns", IsDecColumnsVisible },
                { "Deductions", IsDeductionsVisible },
                { "SupFee", IsSupFeeVisible },
                { "TotalNett", IsTotalNettVisible },
                { "Bank", IsBankVisible },
                { "BankAccount", IsBankAccountVisible },
                { "Comments", IsCommentsVisible },
                { "Notes", IsNotesVisible },
                { "TotalRem", IsTotalRemVisible },
                { "Days", IsDaysVisible }
            };
        }
    }
}
