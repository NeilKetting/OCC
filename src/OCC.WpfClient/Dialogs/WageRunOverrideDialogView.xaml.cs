using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using OCC.WpfClient.Features.AttendanceHub.ViewModels;

namespace OCC.WpfClient.Dialogs
{
    /// <summary>
    /// Interaction logic for WageRunOverrideDialogView.xaml.
    /// Provides direct line-level overrides for employee wages with live preview and mandatory reason logging.
    /// </summary>
    public partial class WageRunOverrideDialogView : Window
    {
        private readonly WageRunLineViewModel _lineViewModel;
        private bool _isInitializing = true;

        public WageRunOverrideDialogView(WageRunLineViewModel lineViewModel)
        {
            InitializeComponent();
            _lineViewModel = lineViewModel ?? throw new ArgumentNullException(nameof(lineViewModel));

            PopulateInitialValues();
            _isInitializing = false;
            RecalculateLivePreview();
        }

        private void PopulateInitialValues()
        {
            EmpNameText.Text = _lineViewModel.EmployeeName;
            EmpNumberText.Text = string.IsNullOrWhiteSpace(_lineViewModel.EmployeeNumber) ? "NO ID" : _lineViewModel.EmployeeNumber;
            EmpBranchText.Text = string.IsNullOrWhiteSpace(_lineViewModel.Branch) ? "General" : _lineViewModel.Branch;
            EmpRateText.Text = $"Rate: R{_lineViewModel.HourlyRate:F2}/hr";
            CurrentNetPayText.Text = $"R {_lineViewModel.NetPay:N2}";
            OriginalNetPayPreviewText.Text = $"R {_lineViewModel.NetPay:N2}";

            // Hours & Rates
            NormalHoursInput.Text = _lineViewModel.NormalHours.ToString("F2", CultureInfo.InvariantCulture);
            VarianceHoursInput.Text = _lineViewModel.VarianceHours.ToString("F2", CultureInfo.InvariantCulture);
            Overtime15Input.Text = _lineViewModel.Overtime15Hours.ToString("F2", CultureInfo.InvariantCulture);
            SaturdayOtInput.Text = _lineViewModel.SaturdayOvertimeHours.ToString("F2", CultureInfo.InvariantCulture);
            Overtime20Input.Text = _lineViewModel.Overtime20Hours.ToString("F2", CultureInfo.InvariantCulture);
            HourlyRateInput.Text = _lineViewModel.HourlyRate.ToString("F2", CultureInfo.InvariantCulture);

            // Deductions & Incentives
            DeductionAdvanceInput.Text = _lineViewModel.DeductionAdvanceRecovery.ToString("F2", CultureInfo.InvariantCulture);
            DeductionLoanInput.Text = _lineViewModel.DeductionLoan.ToString("F2", CultureInfo.InvariantCulture);
            DeductionWashingInput.Text = _lineViewModel.DeductionWashing.ToString("F2", CultureInfo.InvariantCulture);
            DeductionGasInput.Text = _lineViewModel.DeductionGas.ToString("F2", CultureInfo.InvariantCulture);
            DeductionPPEInput.Text = _lineViewModel.DeductionPPE.ToString("F2", CultureInfo.InvariantCulture);
            DeductionOtherInput.Text = _lineViewModel.DeductionOther.ToString("F2", CultureInfo.InvariantCulture);
            IncentiveSupervisorInput.Text = _lineViewModel.IncentiveSupervisor.ToString("F2", CultureInfo.InvariantCulture);
        }

        private void FieldValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            RecalculateLivePreview();
        }

        private void RecalculateLivePreview()
        {
            double normal = ParseDouble(NormalHoursInput.Text);
            double variance = ParseDouble(VarianceHoursInput.Text);
            double ot15 = ParseDouble(Overtime15Input.Text);
            double satOt = ParseDouble(SaturdayOtInput.Text);
            double ot20 = ParseDouble(Overtime20Input.Text);
            decimal rate = ParseDecimal(HourlyRateInput.Text);

            decimal advRec = ParseDecimal(DeductionAdvanceInput.Text);
            decimal loan = ParseDecimal(DeductionLoanInput.Text);
            decimal washing = ParseDecimal(DeductionWashingInput.Text);
            decimal gas = ParseDecimal(DeductionGasInput.Text);
            decimal ppe = ParseDecimal(DeductionPPEInput.Text);
            decimal other = ParseDecimal(DeductionOtherInput.Text);
            decimal supFee = ParseDecimal(IncentiveSupervisorInput.Text);

            // Total Wage = (Normal + Projected + Variance) * Rate + (OT15 + SatOT) * Rate * 1.5 + OT20 * Rate * 2.0
            double projHours = _lineViewModel.Model.ProjectedHours;
            decimal totalWage = (decimal)(normal + projHours + variance) * rate
                              + (decimal)(ot15 + satOt) * rate * 1.5m
                              + (decimal)ot20 * rate * 2.0m;

            // NetPay = TotalWage - Deductions
            decimal totalDeductions = loan + washing + gas + other + ppe + advRec;
            decimal newNetPay = Math.Max(0m, totalWage - totalDeductions);

            decimal diff = newNetPay - _lineViewModel.NetPay;

            NewNetPayPreviewText.Text = $"R {newNetPay:N2}";
            if (diff >= 0)
            {
                NetPayDiffText.Text = $" (+R {diff:N2})";
                NetPayDiffText.Foreground = (System.Windows.Media.Brush?)TryFindResource("SuccessGreen") 
                    ?? (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#34D399")!;
            }
            else
            {
                NetPayDiffText.Text = $" (-R {Math.Abs(diff):N2})";
                NetPayDiffText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
        }

        private void ApplyOverride_Click(object sender, RoutedEventArgs e)
        {
            var reason = ReasonInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(reason))
            {
                ValidationErrorText.Visibility = Visibility.Visible;
                ReasonInput.Focus();
                return;
            }

            ValidationErrorText.Visibility = Visibility.Collapsed;

            // Track changes for note summary
            var changes = new List<string>();

            double newNormal = ParseDouble(NormalHoursInput.Text);
            if (Math.Abs(_lineViewModel.NormalHours - newNormal) > 0.001)
            {
                changes.Add($"Normal Hrs {_lineViewModel.NormalHours:F1} ➔ {newNormal:F1}");
                _lineViewModel.Model.NormalHours = newNormal;
            }

            double newVariance = ParseDouble(VarianceHoursInput.Text);
            if (Math.Abs(_lineViewModel.VarianceHours - newVariance) > 0.001)
            {
                changes.Add($"Var Hrs {_lineViewModel.VarianceHours:F1} ➔ {newVariance:F1}");
                _lineViewModel.Model.VarianceHours = newVariance;
            }

            double newOt15 = ParseDouble(Overtime15Input.Text);
            if (Math.Abs(_lineViewModel.Overtime15Hours - newOt15) > 0.001)
            {
                changes.Add($"OT1.5 {_lineViewModel.Overtime15Hours:F1} ➔ {newOt15:F1}");
                _lineViewModel.Model.Overtime15Hours = newOt15;
            }

            double newSatOt = ParseDouble(SaturdayOtInput.Text);
            if (Math.Abs(_lineViewModel.SaturdayOvertimeHours - newSatOt) > 0.001)
            {
                changes.Add($"Sat OT {_lineViewModel.SaturdayOvertimeHours:F1} ➔ {newSatOt:F1}");
                _lineViewModel.Model.SaturdayOvertimeHours = newSatOt;
            }

            double newOt20 = ParseDouble(Overtime20Input.Text);
            if (Math.Abs(_lineViewModel.Overtime20Hours - newOt20) > 0.001)
            {
                changes.Add($"OT2.0 {_lineViewModel.Overtime20Hours:F1} ➔ {newOt20:F1}");
                _lineViewModel.Model.Overtime20Hours = newOt20;
            }

            decimal newRate = ParseDecimal(HourlyRateInput.Text);
            if (_lineViewModel.HourlyRate != newRate)
            {
                changes.Add($"Rate R{_lineViewModel.HourlyRate:F2} ➔ R{newRate:F2}");
                _lineViewModel.Model.HourlyRate = newRate;
            }

            decimal newAdv = ParseDecimal(DeductionAdvanceInput.Text);
            if (_lineViewModel.DeductionAdvanceRecovery != newAdv)
            {
                changes.Add($"Adv Rec R{_lineViewModel.DeductionAdvanceRecovery:F2} ➔ R{newAdv:F2}");
                _lineViewModel.Model.DeductionAdvanceRecovery = newAdv;
            }

            decimal newLoan = ParseDecimal(DeductionLoanInput.Text);
            if (_lineViewModel.DeductionLoan != newLoan)
            {
                changes.Add($"Loan R{_lineViewModel.DeductionLoan:F2} ➔ R{newLoan:F2}");
                _lineViewModel.Model.DeductionLoan = newLoan;
            }

            decimal newWashing = ParseDecimal(DeductionWashingInput.Text);
            if (_lineViewModel.DeductionWashing != newWashing)
            {
                changes.Add($"Washing R{_lineViewModel.DeductionWashing:F2} ➔ R{newWashing:F2}");
                _lineViewModel.Model.DeductionWashing = newWashing;
            }

            decimal newGas = ParseDecimal(DeductionGasInput.Text);
            if (_lineViewModel.DeductionGas != newGas)
            {
                changes.Add($"Gas R{_lineViewModel.DeductionGas:F2} ➔ R{newGas:F2}");
                _lineViewModel.Model.DeductionGas = newGas;
            }

            decimal newPPE = ParseDecimal(DeductionPPEInput.Text);
            if (_lineViewModel.DeductionPPE != newPPE)
            {
                changes.Add($"PPE R{_lineViewModel.DeductionPPE:F2} ➔ R{newPPE:F2}");
                _lineViewModel.Model.DeductionPPE = newPPE;
            }

            decimal newOther = ParseDecimal(DeductionOtherInput.Text);
            if (_lineViewModel.DeductionOther != newOther)
            {
                changes.Add($"Other R{_lineViewModel.DeductionOther:F2} ➔ R{newOther:F2}");
                _lineViewModel.Model.DeductionOther = newOther;
            }

            decimal newSup = ParseDecimal(IncentiveSupervisorInput.Text);
            if (_lineViewModel.IncentiveSupervisor != newSup)
            {
                changes.Add($"Sup Fee R{_lineViewModel.IncentiveSupervisor:F2} ➔ R{newSup:F2}");
                _lineViewModel.Model.IncentiveSupervisor = newSup;
            }

            // Append override reason to line VarianceNotes
            string changeSummary = changes.Count > 0 ? string.Join(", ", changes) : "Manual Override";
            string auditNote = $"[Override: {changeSummary}. Reason: {reason}]";

            if (string.IsNullOrWhiteSpace(_lineViewModel.Model.VarianceNotes))
            {
                _lineViewModel.Model.VarianceNotes = auditNote;
            }
            else
            {
                _lineViewModel.Model.VarianceNotes = (_lineViewModel.Model.VarianceNotes.Trim() + "; " + auditNote).Trim();
            }

            _lineViewModel.Recalculate();

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static double ParseDouble(string text)
        {
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double val)) return val;
            if (double.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out val)) return val;
            return 0.0;
        }

        private static decimal ParseDecimal(string text)
        {
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val)) return val;
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.CurrentCulture, out val)) return val;
            return 0m;
        }
    }
}
