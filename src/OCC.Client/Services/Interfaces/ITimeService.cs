using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.Client.Services.Interfaces
{
    public interface ITimeService
    {
        Task<IEnumerable<TimeRecord>> GetWeeklyTimeRecordsAsync(DateTime weekStart);
        Task SaveTimeRecordAsync(TimeRecord record);
        Task<IEnumerable<AttendanceRecord>> GetDailyAttendanceAsync(DateTime date);
        Task SaveAttendanceRecordAsync(AttendanceRecord record);
        Task<AttendanceRecord?> GetAttendanceRecordByIdAsync(Guid id);
        Task ClearAllAttendanceAsync();
        Task<IEnumerable<AttendanceRecord>> GetAttendanceByRangeAsync(DateTime startDate, DateTime endDate);
        Task<IEnumerable<AttendanceRecord>> GetActiveAttendanceAsync();
        Task<IEnumerable<Employee>> GetAllStaffAsync();
        Task<string?> UploadDoctorNoteAsync(string localFilePath);
        Task DeleteAttendanceRecordAsync(Guid id);
        Task<Employee?> GetEmployeeByIdAsync(Guid id);
    }
}
