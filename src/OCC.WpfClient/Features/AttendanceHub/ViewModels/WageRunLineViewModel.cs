using CommunityToolkit.Mvvm.ComponentModel;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;
using System;

namespace OCC.WpfClient.Features.AttendanceHub.ViewModels
{
    /// <summary>
    /// Wraps a single WageRunLine for spreadsheet-style editing in the WageRunView DataGrid.
    /// All formulas ported from OCC.Client WageRunLineViewModel.
    /// </summary>
    public partial class WageRunLineViewModel : ObservableObject
    {
        public WageRunLine Model { get; }
        private readonly IDialogService? _dialogService;

        private int? _indexNum;
        public int? IndexNum
        {
            get => _indexNum;
            set => SetProperty(ref _indexNum, value);
        }

        public WageRunLineViewModel(WageRunLine model, IDialogService? dialogService = null)
        {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            _dialogService = dialogService;
        }

        // ─── Display (Read-only columns) ──────────────────────────────────────

        public string Index          => IndexNum?.ToString() ?? string.Empty;
        public string EmployeeNumber => Model.EmployeeNumber ?? string.Empty;
        public string EmployeeName   => Model.EmployeeName?.ToUpper() ?? string.Empty;
        public string Branch         => Model.Branch ?? string.Empty;

        // Rate columns
        public decimal? RatePHrDisplay  => Model.HourlyRate;
        public decimal? StdOtRate       => Model.HourlyRate * 1.5m;
        public decimal? SatOtRate       => Model.HourlyRate * 1.5m;
        public decimal? SunPHolRate     => Model.HourlyRate * 2.0m;
        public decimal DecOtRate        => Model.HourlyRate;
        public double DecOtHrs          => 0.0;
        public decimal DecTotal         => 0.00m;
        public double SatOt             => Model.SaturdayOvertimeHours;

        /// <summary>Rate per day = HourlyRate × 8.75 standard hours</summary>
        public decimal? RatePDayDisplay => Model.HourlyRate * 8.75m;

        // Day counts
        public double DaysWeek1Display => Model.DaysWorkedWeek1;
        public int DaysWeek2Display  => (int)Model.DaysWorkedWeek2;
        public int DaysWeek3Display  => (int)Model.DaysWorkedWeek3;
        public int TotalDaysDisplay  => (int)Model.TotalDaysWorked;

        // Hours per day (standard)
        public double HrsPDayDisplay => 8.75;

        // Sat OT column is always 0 in current payroll logic (Sat = OT15)

        // Deduction display strings (blank if zero, for cleaner grid)
        public string DeductionLoanDisplay    => Model.DeductionLoan    > 0 ? Model.DeductionLoan.ToString("F2")    : string.Empty;
        public string DeductionWashingDisplay => Model.DeductionWashing > 0 ? Model.DeductionWashing.ToString("F2") : string.Empty;
        public string DeductionGasDisplay     => Model.DeductionGas     > 0 ? Model.DeductionGas.ToString("F2")     : string.Empty;
        public string OtherDisplay            => Model.DeductionOther   > 0 ? Model.DeductionOther.ToString("F2")   : string.Empty;
        public string DeductionPPEDisplay     => Model.DeductionPPE     > 0 ? Model.DeductionPPE.ToString("F2")     : string.Empty;
        public string BankAccountNumber       => Model.BankAccountNumber ?? string.Empty;
        public string BankName                => Model.BankName ?? string.Empty;

        public string Comments
        {
            get => Model.Comments ?? string.Empty;
            set
            {
                if (Model.Comments != value)
                {
                    Model.Comments = value;
                    OnPropertyChanged();
                }
            }
        }

        public string VarianceNotes           => Model.VarianceNotes ?? string.Empty;

        // Computed financials
        /// <summary>TotalRem = TotalWage (supervisor fee is excluded as it's a cash incentive)</summary>
        public decimal TotalRem     => Model.TotalWage;
        public decimal NetPay       => Model.NetPay;
        public decimal TotalWage    => Model.TotalWage;
        public decimal HourlyRate   => Model.HourlyRate;
        public double  VarianceHours => Model.VarianceHours;
        public bool   HasSupervisorFee        => Model.IncentiveSupervisor > 0;

        // ─── Editable Properties (trigger recalc) ─────────────────────────────

        public static decimal BibcRate { get; set; } = 28.75m;

        public bool IsBibc
        {
            get => Model.IsBibc;
            set
            {
                if (Model.IsBibc != value)
                {
                    Model.IsBibc = value;
                    RecalculateAndNotify();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(BibcAmount));
                }
            }
        }

        public decimal BibcAmount => Model.BibcAmount;

        /// <summary>Normal weekday hours within shift. Recalculates TotalWage/NetPay.</summary>
        public double NormalHours
        {
            get => Model.NormalHours;
            set
            {
                if (Math.Abs(Model.NormalHours - value) > 0.001)
                {
                    double oldValue = Model.NormalHours;
                    Model.NormalHours = value;
                    RecalculateAndNotify();
                    OnPropertyChanged();
                    PromptReason("Normal Hours", oldValue, value);
                }
            }
        }

        /// <summary>Weekday after-shift overtime hours (1.5×).</summary>
        public double Overtime15Hours
        {
            get => Model.Overtime15Hours;
            set
            {
                if (Math.Abs(Model.Overtime15Hours - value) > 0.001)
                {
                    double oldValue = Model.Overtime15Hours;
                    Model.Overtime15Hours = value;
                    RecalculateAndNotify();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StdOt));
                    PromptReason("OT 1.5 Hours", oldValue, value);
                }
            }
        }

        /// <summary>Sunday / public holiday overtime hours (2.0×).</summary>
        public double Overtime20Hours
        {
            get => Model.Overtime20Hours;
            set
            {
                if (Math.Abs(Model.Overtime20Hours - value) > 0.001)
                {
                    double oldValue = Model.Overtime20Hours;
                    Model.Overtime20Hours = value;
                    RecalculateAndNotify();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SunOt));
                    PromptReason("OT 2.0 Hours", oldValue, value);
                }
            }
        }

        /// <summary>Saturday overtime hours (1.5×).</summary>
        public double SaturdayOvertimeHours
        {
            get => Model.SaturdayOvertimeHours;
            set
            {
                if (Math.Abs(Model.SaturdayOvertimeHours - value) > 0.001)
                {
                    double oldValue = Model.SaturdayOvertimeHours;
                    Model.SaturdayOvertimeHours = value;
                    RecalculateAndNotify();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SatOt));
                    PromptReason("OT Saturday Hours", oldValue, value);
                }
            }
        }

        public decimal DeductionLoan
        {
            get => Model.DeductionLoan;
            set { if (Model.DeductionLoan != value) { Model.DeductionLoan = value; RecalculateAndNotify(); OnPropertyChanged(); OnPropertyChanged(nameof(DeductionLoanDisplay)); } }
        }

        public decimal DeductionWashing
        {
            get => Model.DeductionWashing;
            set { if (Model.DeductionWashing != value) { Model.DeductionWashing = value; RecalculateAndNotify(); OnPropertyChanged(); OnPropertyChanged(nameof(DeductionWashingDisplay)); } }
        }

        public decimal DeductionGas
        {
            get => Model.DeductionGas;
            set { if (Model.DeductionGas != value) { Model.DeductionGas = value; RecalculateAndNotify(); OnPropertyChanged(); OnPropertyChanged(nameof(DeductionGasDisplay)); } }
        }

        public decimal DeductionOther
        {
            get => Model.DeductionOther;
            set { if (Model.DeductionOther != value) { Model.DeductionOther = value; RecalculateAndNotify(); OnPropertyChanged(); OnPropertyChanged(nameof(OtherDisplay)); } }
        }

        public decimal DeductionPPE
        {
            get => Model.DeductionPPE;
            set { if (Model.DeductionPPE != value) { Model.DeductionPPE = value; RecalculateAndNotify(); OnPropertyChanged(); OnPropertyChanged(nameof(DeductionPPEDisplay)); } }
        }

        public decimal DeductionAdvanceRecovery
        {
            get => Model.DeductionAdvanceRecovery;
            set { if (Model.DeductionAdvanceRecovery != value) { Model.DeductionAdvanceRecovery = value; RecalculateAndNotify(); OnPropertyChanged(); OnPropertyChanged(nameof(DeductionAdvanceRecoveryDisplay)); } }
        }

        public string DeductionAdvanceRecoveryDisplay => DeductionAdvanceRecovery > 0 ? $"-R{DeductionAdvanceRecovery:F2}" : "-";

        public decimal IncentiveSupervisor
        {
            get => Model.IncentiveSupervisor;
            set { if (Model.IncentiveSupervisor != value) { Model.IncentiveSupervisor = value; RecalculateAndNotify(); OnPropertyChanged(); OnPropertyChanged(nameof(HasSupervisorFee)); } }
        }

        // ─── Std OT display (read-only summary column) ──────────────────────
        public double StdOt => Model.Overtime15Hours;
        public double SunOt => Model.Overtime20Hours;

        // ─── Standard hours = Normal + Projected + Variance (for display) ────
        public double StdHoursDisplay => Model.NormalHours + Model.ProjectedHours + Model.VarianceHours;

        // ─── Recalculation ───────────────────────────────────────────────────

        /// <summary>
        /// Re-notifies NetPay (computed on the model from all fields) and dependent totals.
        /// Model formula: NetPay = (TotalWage + IncentiveSupervisor) - (Loan + Tax + Washing + Gas + Other + PPE)
        /// where TotalWage = ((NormalHours + ProjectedHours + VarianceHours) × HourlyRate)
        ///                 + (OT15Hours × HourlyRate × 1.5)
        ///                 + (OT20Hours × HourlyRate × 2.0)
        /// </summary>
        public void Recalculate()
        {
            RecalculateAndNotify();
        }

        private void RecalculateAndNotify()
        {
            if (Model == null) return;

            // Recalculate BIBC Amount if applicable
            if (Model.IsBibc && (string.Equals(Model.Branch, "Cape Town", StringComparison.OrdinalIgnoreCase) || string.Equals(Model.Branch, "CPT", StringComparison.OrdinalIgnoreCase)))
            {
                Model.BibcAmount = BibcRate * (decimal)Model.TotalDaysWorked;
            }
            else
            {
                Model.BibcAmount = 0m;
            }

            // Recalculate TotalWage on the model (NetPay is derived from it)
            Model.TotalWage =
                (decimal)(Model.NormalHours + Model.ProjectedHours + Model.VarianceHours) * Model.HourlyRate
                + (decimal)(Model.Overtime15Hours + Model.SaturdayOvertimeHours) * Model.HourlyRate * 1.5m
                + (decimal)Model.Overtime20Hours * Model.HourlyRate * 2.0m;

            // NetPay is a computed property on the model — re-notify all display properties
            OnPropertyChanged(nameof(NetPay));
            OnPropertyChanged(nameof(TotalRem));
            OnPropertyChanged(nameof(TotalWage));
            OnPropertyChanged(nameof(StdHoursDisplay));
            OnPropertyChanged(nameof(BibcAmount));
        }

        private void PromptReason(string hoursType, double oldValue, double newValue)
        {
            if (_dialogService == null) return;

            System.Windows.Application.Current.Dispatcher.BeginInvoke(new System.Action(async () =>
            {
                var title = "Adjustment Reason Required";
                var message = $"You have manually adjusted {hoursType} for {EmployeeName} from {oldValue:F1} to {newValue:F1}.\n\nPlease specify the reason for this change:";
                var reason = await _dialogService.ShowInputDialogAsync(title, message);

                if (string.IsNullOrWhiteSpace(reason))
                {
                    // Revert the value directly on the model to avoid re-triggering the setter
                    if (hoursType == "Normal Hours")
                    {
                        Model.NormalHours = oldValue;
                        OnPropertyChanged(nameof(NormalHours));
                        OnPropertyChanged(nameof(StdHoursDisplay));
                    }
                    else if (hoursType == "OT 1.5 Hours")
                    {
                        Model.Overtime15Hours = oldValue;
                        OnPropertyChanged(nameof(Overtime15Hours));
                        OnPropertyChanged(nameof(StdOt));
                    }
                    else if (hoursType == "OT 2.0 Hours")
                    {
                        Model.Overtime20Hours = oldValue;
                        OnPropertyChanged(nameof(Overtime20Hours));
                        OnPropertyChanged(nameof(SunOt));
                    }
                    RecalculateAndNotify();
                    await _dialogService.ShowAlertAsync("Adjustment Cancelled", "A valid reason is required to adjust hours. The hours have been reverted.");
                }
                else
                {
                    var note = $"[Manual Adj: {hoursType} from {oldValue:F1} to {newValue:F1}. Reason: {reason.Trim()}]";
                    if (string.IsNullOrWhiteSpace(Model.VarianceNotes))
                    {
                        Model.VarianceNotes = note;
                    }
                    else
                    {
                        Model.VarianceNotes = (Model.VarianceNotes + "; " + note).Trim();
                    }
                    OnPropertyChanged(nameof(VarianceNotes));
                }
            }));
        }
    }
}
