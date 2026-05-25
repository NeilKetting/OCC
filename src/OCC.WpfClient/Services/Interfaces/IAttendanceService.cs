using OCC.Shared.Models;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IAttendanceService
    {
        Task<IEnumerable<AttendanceRecord>> GetAttendanceRecordsAsync(DateTime? from = null, DateTime? to = null);
        Task<IEnumerable<AttendanceRecord>> GetTodaysAttendanceAsync();
        Task<AttendanceRecord?> ClockInAsync(Guid employeeId, string branch);
        Task<AttendanceRecord?> ClockOutAsync(Guid recordId);
        Task<AttendanceRecord> CreateAttendanceRecordAsync(AttendanceRecord record);
        Task<bool> UpdateAttendanceRecordAsync(AttendanceRecord record);
        Task<bool> DeleteAttendanceRecordAsync(Guid id);

        Task<IEnumerable<Team>> GetTeamsAsync();
        Task<Team?> GetTeamAsync(Guid id);
        Task<Team?> CreateTeamAsync(Team team);
        Task<bool> UpdateTeamAsync(Team team);
        Task<bool> DeleteTeamAsync(Guid id);

        Task<bool> AddTeamMemberAsync(Guid teamId, Guid employeeId);
        Task<bool> RemoveTeamMemberAsync(Guid teamId, Guid employeeId);
    }
}
