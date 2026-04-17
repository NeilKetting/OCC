using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.Client.Services.Interfaces
{
    public interface IDialogService
    {
        Task<string?> PickFileAsync(string title, IEnumerable<string> extensions);
        Task ShowBugReportAsync(string viewName, string? screenshotBase64 = null);
        Task<bool> ShowConfirmationAsync(string title, string message);
        Task ShowAlertAsync(string title, string message);
        Task<(bool Confirmed, string? Reason, string? Note, string? FilePath)> ShowLeaveEarlyReasonAsync();
        Task<(bool Confirmed, TimeSpan? InTime, TimeSpan? OutTime)> ShowEditAttendanceAsync(TimeSpan? currentIn, TimeSpan? currentOut, bool showIn = true, bool showOut = true);
        Task<bool> ShowSessionTimeoutAsync();
        Task<string?> ShowInputAsync(string title, string message, string defaultValue = "");
        Task<OCC.Shared.Models.EmployeeLoan?> ShowAddLoanAsync();
        Task<LeaveRequest?> ShowEditLeaveRequestAsync(LeaveRequest request);
    }
}
