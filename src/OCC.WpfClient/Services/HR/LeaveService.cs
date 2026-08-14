using System;
using System.Collections.Generic;
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
    public class LeaveService : ILeaveService
    {
        private readonly ILogger<LeaveService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;
        private readonly IEmployeeService _employeeService;
        private readonly JsonSerializerOptions _options;

        public LeaveService(
            ILogger<LeaveService> logger,
            IHttpClientFactory httpClientFactory,
            ConnectionSettings connectionSettings,
            IAuthService authService,
            IEmployeeService employeeService)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _connectionSettings = connectionSettings;
            _authService = authService;
            _employeeService = employeeService;
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

        public async Task<IEnumerable<LeaveRequest>> GetLeaveRequestsAsync()
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/LeaveRequests");
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<LeaveRequest>>(url, _options) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching leave requests");
                return [];
            }
        }

        public async Task<LeaveRequest?> SubmitLeaveRequestAsync(LeaveRequest request)
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/LeaveRequests");
            try
            {
                if (request.Id == Guid.Empty) request.Id = Guid.NewGuid();
                request.CreatedDate = DateTime.UtcNow;
                request.Status = LeaveStatus.Pending;

                var response = await _httpClient.PostAsJsonAsync(url, request, _options);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<LeaveRequest>(_options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting leave request");
                throw;
            }
        }

        public async Task<bool> ApproveLeaveAsync(Guid requestId, string? comment = null)
        {
            EnsureAuthorization();
            try
            {
                // 1. Fetch the leave request
                var getUrl = GetFullUrl($"api/LeaveRequests/{requestId}");
                var request = await _httpClient.GetFromJsonAsync<LeaveRequest>(getUrl, _options);
                if (request == null) return false;

                // 2. Fetch employee to calculate paid/unpaid days split based on balance
                var employee = await _employeeService.GetEmployeeAsync(request.EmployeeId);
                if (employee != null)
                {
                    if (request.LeaveType == LeaveType.CulturalObligations)
                    {
                        double totalDays = request.NumberOfDays;
                        double cappedPaid = Math.Min(3.0, totalDays);
                        double employeeAnnualBalance = employee.AnnualLeaveBalance;

                        request.PaidDays = Math.Max(0, Math.Min(cappedPaid, employeeAnnualBalance));
                        request.UnpaidDays = Math.Max(0, totalDays - request.PaidDays);
                    }
                    else if (request.LeaveType == LeaveType.Sick)
                    {
                        double totalDays = request.NumberOfDays;
                        double availableSick = employee.SickLeaveBalance;
                        request.PaidDays = Math.Max(0, Math.Min(totalDays, availableSick));
                        request.UnpaidDays = Math.Max(0, totalDays - request.PaidDays);
                        request.IsUnpaid = (request.PaidDays == 0);
                    }
                    else if (request.LeaveType == LeaveType.Unpaid || request.LeaveType == LeaveType.AbsentWithoutLeave)
                    {
                        request.PaidDays = 0;
                        request.UnpaidDays = request.NumberOfDays;
                        request.IsUnpaid = true;
                    }
                    else
                    {
                        request.PaidDays = request.NumberOfDays;
                        request.UnpaidDays = 0;
                    }
                }

                // 3. Update status
                request.Status = LeaveStatus.Approved;
                request.ActionedDate = DateTime.UtcNow;
                request.AdminComment = comment;

                var putUrl = GetFullUrl($"api/LeaveRequests/{requestId}");
                var putResponse = await _httpClient.PutAsJsonAsync(putUrl, request, _options);
                if (!putResponse.IsSuccessStatusCode) return false;

                // 3. Deduct balance from employee immediately
                await DeductLeaveBalanceAsync(request);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving leave request {Id}", requestId);
                return false;
            }
        }

        public async Task<bool> RejectLeaveAsync(Guid requestId, string? comment = null)
        {
            EnsureAuthorization();
            try
            {
                var getUrl = GetFullUrl($"api/LeaveRequests/{requestId}");
                var request = await _httpClient.GetFromJsonAsync<LeaveRequest>(getUrl, _options);
                if (request == null) return false;

                request.Status = LeaveStatus.Rejected;
                request.ActionedDate = DateTime.UtcNow;
                request.AdminComment = comment;

                var putUrl = GetFullUrl($"api/LeaveRequests/{requestId}");
                var response = await _httpClient.PutAsJsonAsync(putUrl, request, _options);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting leave request {Id}", requestId);
                return false;
            }
        }

        public async Task<bool> DeleteLeaveRequestAsync(Guid requestId)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/LeaveRequests/{requestId}");
            try
            {
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting leave request {Id}", requestId);
                return false;
            }
        }

        public async Task<bool> UpdateLeaveRequestAsync(LeaveRequest request)
        {
            EnsureAuthorization();
            try
            {
                var url = GetFullUrl($"api/LeaveRequests/{request.Id}");
                var response = await _httpClient.PutAsJsonAsync(url, request, _options);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating leave request {Id}", request.Id);
                return false;
            }
        }

        public int CalculateBusinessDays(DateTime start, DateTime end)
        {
            if (end < start) return 0;
            int days = 0;
            for (var d = start.Date; d <= end.Date; d = d.AddDays(1))
            {
                if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                    days++;
            }
            return days;
        }

        // ─── Private Helpers ──────────────────────────────────────────────────

        private async Task DeductLeaveBalanceAsync(LeaveRequest request)
        {
            try
            {
                var emp = await _employeeService.GetEmployeeAsync(request.EmployeeId);
                if (emp == null) return;

                double days = request.NumberOfDays > 0
                    ? request.NumberOfDays
                    : CalculateBusinessDays(request.StartDate, request.EndDate);

                if (days <= 0) return;

                var dto = await _employeeService.GetEmployeeAsync(emp.Id);
                if (dto == null) return;

                switch (request.LeaveType)
                {
                    case LeaveType.Annual:
                    case LeaveType.CulturalObligations:
                    case LeaveType.HalfDay:
                    case LeaveType.Other:
                        dto.AnnualLeaveBalance = Math.Max(0, emp.AnnualLeaveBalance - request.PaidDays);
                        break;
                    case LeaveType.Sick:
                        dto.SickLeaveBalance = Math.Max(0, emp.SickLeaveBalance - request.PaidDays);
                        break;
                    // Maternity, Study, FamilyResponsibility, Unpaid — no balance to deduct
                }

                var fullEmp = new OCC.WpfClient.Features.EmployeeHub.Models.EmployeeModel(dto).ToEntity();
                await _employeeService.UpdateEmployeeAsync(fullEmp);
                _logger.LogInformation("Deducted {Days} {Type} leave day(s) from employee {Id}", days, request.LeaveType, emp.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not deduct leave balance for employee {Id} on approval", request.EmployeeId);
            }
        }
    }
}
