using OCC.Shared.Models;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface ILeaveService
    {
        Task<IEnumerable<LeaveRequest>> GetLeaveRequestsAsync();
        Task<LeaveRequest?> SubmitLeaveRequestAsync(LeaveRequest request);
        Task<bool> ApproveLeaveAsync(Guid requestId, string? comment = null);
        Task<bool> RejectLeaveAsync(Guid requestId, string? comment = null);
        Task<bool> DeleteLeaveRequestAsync(Guid requestId);
        Task<bool> UpdateLeaveRequestAsync(LeaveRequest request);

        /// <summary>
        /// Calculates the number of working days (Mon–Fri) between two dates inclusive.
        /// </summary>
        int CalculateBusinessDays(DateTime start, DateTime end);
    }
}
