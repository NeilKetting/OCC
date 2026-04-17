using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;

using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;

namespace OCC.Client.Services
{
    public class TimeService : ITimeService
    {
        private readonly IRepository<TimeRecord> _timeRepository;
        private readonly IRepository<AttendanceRecord> _attendanceRepository;
        private readonly IRepository<Employee> _staffRepository;
        private readonly IAuthService _authService; // Added

        public TimeService(
            IRepository<TimeRecord> timeRepository,
            IRepository<AttendanceRecord> attendanceRepository,
            IRepository<Employee> staffRepository,
            IAuthService authService) // Added
        {
            _timeRepository = timeRepository;
            _attendanceRepository = attendanceRepository;
            _staffRepository = staffRepository;
            _authService = authService;
        }

        public async Task<IEnumerable<TimeRecord>> GetWeeklyTimeRecordsAsync(DateTime weekStart)
        {
            var weekEnd = weekStart.AddDays(7);
            return await _timeRepository.FindAsync(r => r.Date >= weekStart && r.Date < weekEnd);
        }

        public async Task SaveTimeRecordAsync(TimeRecord record)
        {
            if (record.Id == Guid.Empty || (await _timeRepository.GetByIdAsync(record.Id)) == null)
            {
                await _timeRepository.AddAsync(record);
            }
            else
            {
                await _timeRepository.UpdateAsync(record);
            }
        }

        public async Task<IEnumerable<AttendanceRecord>> GetDailyAttendanceAsync(DateTime date)
        {
            var dayStart = date.Date;
            var dayEnd = dayStart.AddDays(1);
            return await _attendanceRepository.FindAsync(r => r.Date >= dayStart && r.Date < dayEnd);
        }

        public async Task<IEnumerable<AttendanceRecord>> GetAttendanceByRangeAsync(DateTime startDate, DateTime endDate)
        {
            // Ensure end date is inclusive of the day (e.g., if user picks 2026-01-08, we want up to 2026-01-08 23:59:59)
            // If the incoming View Logic gives "Today" as start:Today, end:Today, we want full day.
            // Usually best to treat EndDate as "End of Day" or next day 00:00.
            // But let's assume the VM gives strict boundaries, or we standardize here.
            
            // Standardizing: Date portion comparison or simple range.
            // Best practice: >= Start AND < End (where End is +1 day of the visualization range).
            
            return await _attendanceRepository.FindAsync(r => r.Date >= startDate && r.Date <= endDate);
        }

        public async Task<IEnumerable<AttendanceRecord>> GetActiveAttendanceAsync()
        {
            var today = DateTime.Today;
            return await _attendanceRepository.FindAsync(r => 
                r.Status != AttendanceStatus.Absent && 
                ((r.Date.Date == today && (r.CheckOutTime == null || r.CheckOutTime == DateTime.MinValue)) ||
                 (r.Date.Date < today && r.CheckInTime != null && (r.CheckOutTime == null || r.CheckOutTime == DateTime.MinValue)))
            );
        }

        public async Task<AttendanceRecord?> GetAttendanceRecordByIdAsync(Guid id)
        {
            return await _attendanceRepository.GetByIdAsync(id);
        }

        public async Task SaveAttendanceRecordAsync(AttendanceRecord record)
        {
            if (record.Id == Guid.Empty || (await _attendanceRepository.GetByIdAsync(record.Id)) == null)
            {
                await _attendanceRepository.AddAsync(record);
            }
            else
            {
                await _attendanceRepository.UpdateAsync(record);
            }
        }

        public async Task ClearAllAttendanceAsync()
        {
            try
            {
                // For development: fetch all and delete.
                var allRecords = await _attendanceRepository.GetAllAsync();
                foreach (var record in allRecords)
                {
                    await _attendanceRepository.DeleteAsync(record.Id);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error clearing attendance: {ex.Message}");
                // In production, log properly
            }
        }

        public async Task<IEnumerable<Employee>> GetAllStaffAsync()
        {
            return await _staffRepository.GetAllAsync();
        }

        public async Task<string?> UploadDoctorNoteAsync(string localFilePath)
        {
            if (string.IsNullOrEmpty(localFilePath) || !System.IO.File.Exists(localFilePath)) return null;

            try
            {
                var baseUrl = OCC.Client.Services.Infrastructure.ConnectionSettings.Instance.ApiBaseUrl;
                if (!baseUrl.EndsWith("/")) baseUrl += "/";

                using var client = new System.Net.Http.HttpClient { BaseAddress = new Uri(baseUrl) };
                
                // Add Auth
                var token = _authService.AuthToken;
                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                using var content = new System.Net.Http.MultipartFormDataContent();
                var fileContent = new System.Net.Http.ByteArrayContent(await System.IO.File.ReadAllBytesAsync(localFilePath));
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream"); // Or try to guess mime type
                
                // "file" must match the parameter name in the Controller
                content.Add(fileContent, "file", System.IO.Path.GetFileName(localFilePath));

                var response = await client.PostAsync("api/AttendanceRecords/upload", content);
                response.EnsureSuccessStatusCode();

                // API returns the relative path as string (json formatted string usually, or just raw string if strictly strictly "text/plain" but API returns ActionResult<string> so likely JSON string e.g. "/path" or plain path)
                // If it returns Ok(string), it might be treated as text.
                // Let's read as string.
                var result = await response.Content.ReadAsStringAsync();
                
                // If API returns JSON string (e.g. "/uploads/..."), Trim quotes.
                return result.Trim('"'); 
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error uploading note: {ex.Message}");
                // throw? or return null
                return null;
            }
            }


        public async Task DeleteAttendanceRecordAsync(Guid id)
        {
            await _attendanceRepository.DeleteAsync(id);
        }

        public async Task<Employee?> GetEmployeeByIdAsync(Guid id)
        {
            return await _staffRepository.GetByIdAsync(id);
        }
    }
}
