using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System.Web;

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
            var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5237/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        public async Task<AuditLogsResponseDto?> GetAuditLogsAsync(
            string? search, 
            Guid? userId, 
            DateTime? startDate, 
            DateTime? endDate, 
            int skip, 
            int take)
        {
            EnsureAuthorization();
            
            var query = HttpUtility.ParseQueryString(string.Empty);
            if (!string.IsNullOrEmpty(search)) query["search"] = search;
            if (userId.HasValue && userId.Value != Guid.Empty) query["userId"] = userId.Value.ToString();
            if (startDate.HasValue) query["startDate"] = startDate.Value.ToString("yyyy-MM-dd");
            if (endDate.HasValue) query["endDate"] = endDate.Value.ToString("yyyy-MM-dd");
            query["skip"] = skip.ToString();
            query["take"] = take.ToString();

            var url = GetFullUrl($"api/Audit?{query}");
            try
            {
                return await _httpClient.GetFromJsonAsync<AuditLogsResponseDto>(url, _options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching audit logs from {Url}", url);
                return null;
            }
        }

        public async Task<int> GetTotalCountAsync()
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/Audit/count");
            try
            {
                return await _httpClient.GetFromJsonAsync<int>(url, _options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching audit logs total count from {Url}", url);
                return 0;
            }
        }
    }
}
