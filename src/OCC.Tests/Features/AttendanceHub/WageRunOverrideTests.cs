using System;
using Xunit;
using OCC.Shared.Models;
using OCC.WpfClient.Features.AttendanceHub.ViewModels;

namespace OCC.Tests.Features.AttendanceHub
{
    /// <summary>
    /// Unit tests for Wage Run employee line override calculation and note audit trail logic.
    /// </summary>
    public class WageRunOverrideTests
    {
        [Fact]
        public void Recalculate_UpdatesTotalWageAndNetPay_WhenHoursAreOverridden()
        {
            // Arrange: Employee with 80 normal hours @ R50/hr
            var lineModel = new WageRunLine
            {
                EmployeeId = Guid.NewGuid(),
                EmployeeName = "Jane Doe",
                EmployeeNumber = "BAS-101",
                HourlyRate = 50.00m,
                NormalHours = 80.0, // 80 hrs * R50 = R4,000
                DeductionLoan = 200.00m
            };

            var vm = new WageRunLineViewModel(lineModel);
            vm.Recalculate();

            Assert.Equal(4000.00m, vm.TotalWage);
            Assert.Equal(3800.00m, vm.NetPay);

            // Act: Override NormalHours by adding +17.5 hrs (2 days @ 8.75 hrs/day paid in advance previously missed)
            vm.NormalHours = 97.5; // 97.5 hrs * R50 = R4,875
            vm.Recalculate();

            // Assert: TotalWage = R4,875, NetPay = R4,875 - R200 = R4,675
            Assert.Equal(4875.00m, vm.TotalWage);
            Assert.Equal(4675.00m, vm.NetPay);
        }

        [Fact]
        public void Recalculate_UpdatesNetPay_WhenAdvanceRecoveryAndSupervisorFeeOverridden()
        {
            // Arrange
            var lineModel = new WageRunLine
            {
                EmployeeId = Guid.NewGuid(),
                EmployeeName = "John Smith",
                HourlyRate = 100.00m,
                NormalHours = 40.0, // R4,000
                DeductionAdvanceRecovery = 500.00m, // -R500
                IncentiveSupervisor = 300.00m
            };

            var vm = new WageRunLineViewModel(lineModel);
            vm.Recalculate();

            // NetPay = TotalWage - Deductions = 4000 - 500 = 3500
            Assert.Equal(4000.00m, vm.TotalWage);
            Assert.Equal(3500.00m, vm.NetPay);

            // Act: Adjust Advance Recovery to 0 and add PPE deduction
            vm.DeductionAdvanceRecovery = 0m;
            vm.DeductionPPE = 150.00m;
            vm.Recalculate();

            // NetPay = 4000 - 150 = 3850
            Assert.Equal(3850.00m, vm.NetPay);
        }

        [Fact]
        public void VarianceNotes_AppendsOverrideReason_PreservingExistingNotes()
        {
            // Arrange
            var lineModel = new WageRunLine
            {
                EmployeeId = Guid.NewGuid(),
                EmployeeName = "Test Worker",
                HourlyRate = 60.00m,
                NormalHours = 40.0,
                VarianceNotes = "Prior absence flagged on Tuesday."
            };

            var vm = new WageRunLineViewModel(lineModel);

            // Act: Append audit override reason
            string reason = "Paid 2 advance days (17.5 hrs) deducted in prior run due to late finalization";
            string auditNote = $"[Override: Normal Hrs 40.0 ➔ 57.5. Reason: {reason}]";

            if (string.IsNullOrWhiteSpace(vm.Model.VarianceNotes))
            {
                vm.Model.VarianceNotes = auditNote;
            }
            else
            {
                vm.Model.VarianceNotes = (vm.Model.VarianceNotes.Trim() + "; " + auditNote).Trim();
            }

            // Assert
            Assert.Contains("Prior absence flagged on Tuesday.", vm.VarianceNotes);
            Assert.Contains("Paid 2 advance days (17.5 hrs) deducted in prior run due to late finalization", vm.VarianceNotes);
            Assert.StartsWith("Prior absence flagged on Tuesday.; [Override:", vm.VarianceNotes);
        }

        [Fact]
        public void Recalculate_DeductsBibcFromNetPay_ForCapeTownBranchOnly()
        {
            // Arrange CPT line with BIBC enabled
            var cptLine = new WageRunLine
            {
                EmployeeId = Guid.NewGuid(),
                EmployeeName = "CPT Worker",
                Branch = "Cape Town",
                HourlyRate = 50.00m,
                NormalHours = 40.0, // TotalWage = R2000
                TotalDaysWorked = 5,
                IsBibc = true
            };
            var cptVm = new WageRunLineViewModel(cptLine);
            cptVm.Recalculate();

            // BIBC Amount = 28.75 * 5 = 143.75
            // NetPay = 2000 - 143.75 = 1856.25
            Assert.Equal(143.75m, cptVm.BibcAmount);
            Assert.Equal(1856.25m, cptVm.NetPay);

            // Arrange JHB line with BIBC enabled
            var jhbLine = new WageRunLine
            {
                EmployeeId = Guid.NewGuid(),
                EmployeeName = "JHB Worker",
                Branch = "Johannesburg",
                HourlyRate = 50.00m,
                NormalHours = 40.0, // TotalWage = R2000
                TotalDaysWorked = 5,
                IsBibc = true
            };
            var jhbVm = new WageRunLineViewModel(jhbLine);
            jhbVm.Recalculate();

            // JHB: BIBC Amount = 0, NetPay = 2000
            Assert.Equal(0m, jhbVm.BibcAmount);
            Assert.Equal(2000.00m, jhbVm.NetPay);
        }

        [Fact]
        public void Recalculate_UpdatesTotalDaysWorkedAndBibc_WhenDaysWorkedWeek1IsModified()
        {
            // Arrange: CPT employee with -2 W0 DED (both advance days absent initially)
            var cptLine = new WageRunLine
            {
                EmployeeId = Guid.NewGuid(),
                EmployeeName = "Xavier Fester",
                Branch = "Cape Town",
                HourlyRate = 42.60m,
                NormalHours = 42.50,
                DaysWorkedWeek1 = -2.0,
                DaysWorkedWeek2 = 5.0,
                DaysWorkedWeek3 = 0.0,
                TotalDaysWorked = 3.0, // -2 + 5 = 3
                VarianceHours = -17.00,
                IsBibc = true
            };
            var vm = new WageRunLineViewModel(cptLine);
            vm.Recalculate();

            // Initial: 3 days * 28.75 = 86.25 BIBC
            Assert.Equal(3.0, vm.TotalDaysDisplay);
            Assert.Equal(-2.0, vm.DaysWeek1Display);
            Assert.Equal(86.25m, vm.BibcAmount);

            // Act: Change DaysWorkedWeek1 to -1 (Thursday checked as present)
            vm.Model.DaysWorkedWeek1 = -1.0;
            vm.Model.TotalDaysWorked = vm.Model.DaysWorkedWeek1 + vm.Model.DaysWorkedWeek2 + vm.Model.DaysWorkedWeek3; // -1 + 5 = 4 days
            vm.Model.VarianceHours = -8.50;
            vm.Recalculate();

            // Assert: TotalDays = 4, W0 DED = -1, BIBC = 4 * 28.75 = 115.00
            Assert.Equal(4.0, vm.TotalDaysDisplay);
            Assert.Equal(-1.0, vm.DaysWeek1Display);
            Assert.Equal(115.00m, vm.BibcAmount);
        }
    }
}
