using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using OCC.Client.Services.Infrastructure;

namespace OCC.Client.Services
{
    public class TimeServiceV2 : ITimeServiceV2
    {
        private readonly IAuthService _authService;
        private readonly string _baseUrl;

        public TimeServiceV2(IAuthService authService)
        {
            _authService = authService;
            _baseUrl = ConnectionSettings.Instance.ApiBaseUrl;
            if (!_baseUrl.EndsWith("/")) _baseUrl += "/";
        }

        private HttpClient CreateClient()
        {
            var client = new HttpClient { BaseAddress = new Uri(_baseUrl) };
            var token = _authService.AuthToken;
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            return client;
        }

        public async Task<ClockingEvent?> ClockInAsync(Guid employeeId, DateTime? timestamp = null, string source = "WebPortal")
        {
            using var client = CreateClient();
            var payload = new { EmployeeId = employeeId, Timestamp = timestamp, Source = source };
            var response = await client.PostAsJsonAsync("api/ClockingV2/clock-in", payload);
            
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<ClockingEvent>();
            
            return null;
        }

        public async Task<ClockingEvent?> ClockOutAsync(Guid employeeId, DateTime? timestamp = null, string source = "WebPortal")
        {
            using var client = CreateClient();
            var payload = new { EmployeeId = employeeId, Timestamp = timestamp, Source = source };
            var response = await client.PostAsJsonAsync("api/ClockingV2/clock-out", payload);
            
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<ClockingEvent>();
            
            return null;
        }

        public async Task<IEnumerable<ClockingEvent>> GetActivePhysicalPresenceAsync()
        {
            using var client = CreateClient();
            // We will need a specific endpoint for this in the V2 controller
            var response = await client.GetAsync("api/ClockingV2/active");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<IEnumerable<ClockingEvent>>();
                return result ?? new List<ClockingEvent>();
            }
            return new List<ClockingEvent>();
        }

        public async Task<IEnumerable<DailyTimesheet>> GetDailyTimesheetsAsync(DateTime date)
        {
             using var client = CreateClient();
             var response = await client.GetAsync($"api/ClockingV2/timesheets?date={date:yyyy-MM-dd}");
             if (response.IsSuccessStatusCode)
             {
                 var result = await response.Content.ReadFromJsonAsync<IEnumerable<DailyTimesheet>>();
                 return result ?? new List<DailyTimesheet>();
             }
             return new List<DailyTimesheet>();
        }

        public async Task<IEnumerable<DailyTimesheet>> GetTimesheetsByRangeAsync(DateTime startDate, DateTime endDate)
        {
            using var client = CreateClient();
            var response = await client.GetAsync($"api/ClockingV2/timesheets/range?start={startDate:yyyy-MM-dd}&end={endDate:yyyy-MM-dd}");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<IEnumerable<DailyTimesheet>>();
                return result ?? new List<DailyTimesheet>();
            }
            return new List<DailyTimesheet>();
        }

        public async Task<(bool Success, string Message, int Count)> RepairSyncV2Async()
        {
            using var client = CreateClient();
            var response = await client.PostAsync("api/ClockingV2/repair-sync-v2", null);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<SyncResult>();
                return (true, result?.Message ?? "Success", result?.Count ?? 0);
            }
            return (false, "Failed to call repair endpoint", 0);
        }

        private class SyncResult
        {
            public string Message { get; set; } = string.Empty;
            public int Count { get; set; }
        }
    }
}
