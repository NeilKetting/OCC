using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.Client.Services.Interfaces
{
    public interface ILeaveService
    {
        Task<IEnumerable<PublicHoliday>> GetPublicHolidaysAsync(int year);
        Task<IEnumerable<LeaveRequest>> GetEmployeeRequestsAsync(Guid employeeId);
        Task<IEnumerable<LeaveRequest>> GetPendingRequestsAsync();
        
        /// <summary>
        /// Gets all approved leave requests that active on the specific date.
        /// </summary>
        Task<IEnumerable<LeaveRequest>> GetApprovedRequestsForDateAsync(DateTime date);
        
        Task SubmitRequestAsync(LeaveRequest request);
        Task ApproveRequestAsync(Guid requestId, Guid approverId);
        Task RejectRequestAsync(Guid requestId, Guid approverId, string reason);
        Task UpdateRequestAsync(LeaveRequest request);
        Task DeleteRequestAsync(Guid requestId);
        
        /// <summary>
        /// Calculates business days between two dates, excluding weekends and public holidays.
        /// </summary>
        Task<int> CalculateBusinessDaysAsync(DateTime start, DateTime end);

        /// <summary>
        /// Calculates Annual Leave accrual based on BCEA (1 day per 17 worked).
        /// </summary>
        double CalculateAnnualLeaveAccrual(double daysWorked);

        /// <summary>
        /// Calculates Sick Leave accrual for first 6 months (1 day per 26 worked).
        /// </summary>
        double CalculateSickLeaveAccrual(double daysWorked);
        
        /// <summary>
        /// Calculates the current real-time leave balance for an employee.
        /// Formula: (Initial Balance) + (Accrued: 1 day / 17 worked) - (Approved Taken)
        /// </summary>
        Task<double> CalculateCurrentLeaveBalanceAsync(Guid employeeId);
        Task<double> CalculateCurrentLeaveBalanceAsync(Employee employee);
    }
}
