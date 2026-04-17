using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;
using OCC.Client.Services.Infrastructure;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiAuditLogService : IAuditLogService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;

        public ApiAuditLogService(IAuthService authService)
        {
            _authService = authService;
            _httpClient = new HttpClient();
            
            var baseUrl = ConnectionSettings.Instance.ApiBaseUrl;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            _httpClient.BaseAddress = new Uri(baseUrl);
        }

        private void EnsureAuthorization()
        {
            var token = _authService.AuthToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync()
        {
            EnsureAuthorization();
            try
            {
                var result = await _httpClient.GetFromJsonAsync<IEnumerable<AuditLog>>("api/Audit");
                return result ?? Enumerable.Empty<AuditLog>();
            }
            catch (Exception ex)
            {
                // Simple error handling for now, can be improved with logging
                System.Diagnostics.Debug.WriteLine($"Error fetching audit logs: {ex.Message}");
                return Enumerable.Empty<AuditLog>();
            }
        }
    }
}
