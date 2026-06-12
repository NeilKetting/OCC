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
    public class AuditLogService : IAuditLogService
    {
        private readonly ILogger<AuditLogService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;
        private readonly JsonSerializerOptions _options;

        public AuditLogService(ILogger<AuditLogService> logger,
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
                PropertyNameCaseInsensitive = true
            };
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5000/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        public async Task<IEnumerable<AuditLog>> GetAuditLogsAsync()
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/Audit");
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<AuditLog>>(url, _options)
                       ?? new List<AuditLog>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching audit logs from {Url}", url);
                return new List<AuditLog>();
            }
        }
    }
}
