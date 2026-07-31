using OCC.Shared.Models;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IAttendanceService
    {
        Task<IEnumerable<AttendanceRecord>> GetAttendanceRecordsAsync(DateTime? from = null, DateTime? to = null, int? take = null, int? skip = null);
        Task<double> GetProjectSafeHoursAsync(Guid projectId);
        Task<IEnumerable<AttendanceRecord>> GetTodaysAttendanceAsync();
        Task<AttendanceRecord?> ClockInAsync(Guid employeeId, string branch);
        Task<AttendanceRecord?> ClockOutAsync(Guid recordId);
        Task<AttendanceRecord> CreateAttendanceRecordAsync(AttendanceRecord record);
        Task<bool> UpdateAttendanceRecordAsync(AttendanceRecord record);
        Task<bool> DeleteAttendanceRecordAsync(Guid id);

        /// <summary>Marks an employee absent for today — removes any open auto-clockin and creates a closed Absent record.</summary>
        Task<bool> MarkAbsentAsync(Guid employeeId, string branch);

        /// <summary>Uploads a sick note / doctor's note file and returns the server path.</summary>
        Task<string?> UploadSickNoteAsync(string localFilePath);

        Task<IEnumerable<Team>> GetTeamsAsync();
        Task<Team?> GetTeamAsync(Guid id);
        Task<Team?> CreateTeamAsync(Team team);
        Task<bool> UpdateTeamAsync(Team team);
        Task<bool> DeleteTeamAsync(Guid id);

        Task<bool> AddTeamMemberAsync(Guid teamId, Guid employeeId);
        Task<bool> RemoveTeamMemberAsync(Guid teamId, Guid employeeId);
    }
}
