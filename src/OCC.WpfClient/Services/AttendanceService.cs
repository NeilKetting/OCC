using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Services
{
    public class AttendanceService : IAttendanceService
    {
        private readonly ILogger<AttendanceService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;
        private readonly JsonSerializerOptions _options;

        public AttendanceService(
            ILogger<AttendanceService> logger,
            IHttpClientFactory httpClientFactory,
            ConnectionSettings connectionSettings,
            IAuthService authService)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _connectionSettings = connectionSettings;
            _authService = authService;
            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            };
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5000/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        // ─── Attendance Records ───────────────────────────────────────────────

        public async Task<IEnumerable<AttendanceRecord>> GetAttendanceRecordsAsync(DateTime? from = null, DateTime? to = null)
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/AttendanceRecords");
            if (from.HasValue || to.HasValue)
            {
                var qs = new List<string>();
                if (from.HasValue) qs.Add($"from={from.Value:yyyy-MM-dd}");
                if (to.HasValue) qs.Add($"to={to.Value:yyyy-MM-dd}");
                url += "?" + string.Join("&", qs);
            }
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<AttendanceRecord>>(url, _options) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching attendance records");
                return [];
            }
        }

        public async Task<double> GetProjectSafeHoursAsync(Guid projectId)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/HseqStats/project/{projectId}");
            try
            {
                return await _httpClient.GetFromJsonAsync<double>(url, _options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching project safe hours for project {ProjectId}", projectId);
                return 0;
            }
        }

        public async Task<IEnumerable<AttendanceRecord>> GetTodaysAttendanceAsync()
        {
            var today = DateTime.Today;
            return await GetAttendanceRecordsAsync(today, today);
        }

        public async Task<AttendanceRecord?> ClockInAsync(Guid employeeId, string branch)
        {
            EnsureAuthorization();
            var now = DateTime.Now;
            var record = new AttendanceRecord
            {
                EmployeeId = employeeId,
                Date = now.Date,
                CheckInTime = now,
                ClockInTime = now.TimeOfDay,
                Branch = branch,
                Status = AttendanceStatus.Present
            };
            return await CreateAttendanceRecordAsync(record);
        }

        public async Task<AttendanceRecord?> ClockOutAsync(Guid recordId)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/AttendanceRecords/{recordId}");
            try
            {
                var existing = await _httpClient.GetFromJsonAsync<AttendanceRecord>(url, _options);
                if (existing == null) return null;
                existing.CheckOutTime = DateTime.Now;
                await UpdateAttendanceRecordAsync(existing);
                return existing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clocking out record {Id}", recordId);
                return null;
            }
        }

        public async Task<AttendanceRecord> CreateAttendanceRecordAsync(AttendanceRecord record)
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/AttendanceRecords");
            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, record, _options);
                response.EnsureSuccessStatusCode();
                return (await response.Content.ReadFromJsonAsync<AttendanceRecord>(_options))!;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating attendance record");
                throw;
            }
        }

        public async Task<bool> UpdateAttendanceRecordAsync(AttendanceRecord record)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/AttendanceRecords/{record.Id}");
            try
            {
                var response = await _httpClient.PutAsJsonAsync(url, record, _options);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating attendance record {Id}", record.Id);
                return false;
            }
        }

        public async Task<bool> DeleteAttendanceRecordAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/AttendanceRecords/{id}");
            try
            {
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attendance record {Id}", id);
                return false;
            }
        }

        public async Task<bool> MarkAbsentAsync(Guid employeeId, string branch)
        {
            EnsureAuthorization();
            try
            {
                // 1. Delete any open (auto-clocked) record for today
                var today = DateTime.Today;
                var todayRecords = await GetAttendanceRecordsAsync(today, today);
                var openRecord = todayRecords.FirstOrDefault(r => r.EmployeeId == employeeId && r.CheckOutTime == null);
                if (openRecord != null)
                {
                    await DeleteAttendanceRecordAsync(openRecord.Id);
                }

                // 2. Check there's not already a closed record for today (avoid duplicates)
                var existingClosed = todayRecords.FirstOrDefault(r => r.EmployeeId == employeeId && r.CheckOutTime != null);
                if (existingClosed != null && existingClosed.Status == AttendanceStatus.Absent)
                    return true; // Already marked absent

                // 3. Create a closed Absent record
                var absentRecord = new AttendanceRecord
                {
                    Id = Guid.NewGuid(),
                    EmployeeId = employeeId,
                    Date = today,
                    Branch = branch,
                    Status = AttendanceStatus.Absent,
                    CheckInTime = null,
                    CheckOutTime = today, // Closed immediately — won't show as live
                    HoursWorked = 0,
                    Notes = "Marked absent by office."
                };
                await CreateAttendanceRecordAsync(absentRecord);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking employee {Id} absent", employeeId);
                return false;
            }
        }

        public async Task<string?> UploadSickNoteAsync(string localFilePath)
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/AttendanceRecords/upload");
            try
            {
                using var form = new MultipartFormDataContent();
                var fileBytes = await File.ReadAllBytesAsync(localFilePath);
                var fileName = Path.GetFileName(localFilePath);
                form.Add(new ByteArrayContent(fileBytes), "file", fileName);

                var response = await _httpClient.PostAsync(url, form);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading sick note from {Path}", localFilePath);
                return null;
            }
        }

        // ─── Teams ────────────────────────────────────────────────────────────

        public async Task<IEnumerable<Team>> GetTeamsAsync()
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/Teams");
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<Team>>(url, _options) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching teams");
                return [];
            }
        }

        public async Task<Team?> GetTeamAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Teams/{id}");
            try
            {
                return await _httpClient.GetFromJsonAsync<Team>(url, _options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching team {Id}", id);
                return null;
            }
        }

        public async Task<Team?> CreateTeamAsync(Team team)
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/Teams");
            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, team, _options);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<Team>(_options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating team");
                throw;
            }
        }

        public async Task<bool> UpdateTeamAsync(Team team)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Teams/{team.Id}");
            try
            {
                var response = await _httpClient.PutAsJsonAsync(url, team, _options);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating team {Id}", team.Id);
                return false;
            }
        }

        public async Task<bool> DeleteTeamAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Teams/{id}");
            try
            {
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting team {Id}", id);
                return false;
            }
        }

        public async Task<bool> AddTeamMemberAsync(Guid teamId, Guid employeeId)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Teams/{teamId}/members/{employeeId}");
            try
            {
                var response = await _httpClient.PostAsync(url, null);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding member {EmpId} to team {TeamId}", employeeId, teamId);
                return false;
            }
        }

        public async Task<bool> RemoveTeamMemberAsync(Guid teamId, Guid employeeId)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Teams/{teamId}/members/{employeeId}");
            try
            {
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing member {EmpId} from team {TeamId}", employeeId, teamId);
                return false;
            }
        }
    }
}
