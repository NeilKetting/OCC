using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Infrastructure;

namespace OCC.WpfClient.Services
{
    public class BugReportService : IBugReportService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<BugReportService> _logger;
        private readonly ConnectionSettings _connectionSettings;

        public BugReportService(
            IHttpClientFactory httpClientFactory,
            IAuthService authService,
            IPermissionService permissionService,
            ILogger<BugReportService> logger,
            ConnectionSettings connectionSettings)
        {
            _httpClient = httpClientFactory.CreateClient();
            _authService = authService;
            _permissionService = permissionService;
            _logger = logger;
            _connectionSettings = connectionSettings;
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _connectionSettings.ApiBaseUrl;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task SubmitBugAsync(BugReport report)
        {
            try
            {
                EnsureAuthorization();
                var url = GetFullUrl("api/BugReports");
                _logger.LogInformation("Submitting bug report to {Url}", url);
                
                var response = await _httpClient.PostAsJsonAsync(url, report);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting bug report");
                throw;
            }
        }

        public async Task<IEnumerable<BugReport>> GetBugReportsAsync(bool includeArchived = false)
        {
            try
            {
                EnsureAuthorization();
                var url = GetFullUrl($"api/BugReports?includeArchived={includeArchived}");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<BugReport>>() ?? new List<BugReport>();
                }
                
                return new List<BugReport>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bug reports");
                return new List<BugReport>();
            }
        }

        public async Task<IEnumerable<BugReport>> SearchSolutionsAsync(string query)
        {
            try
            {
                var url = GetFullUrl($"api/BugReports/solutions?q={Uri.EscapeDataString(query)}");
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<IEnumerable<BugReport>>() ?? new List<BugReport>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching solutions");
                return new List<BugReport>();
            }
        }

        public async Task<BugReport?> GetBugReportAsync(Guid id)
        {
            try
            {
                EnsureAuthorization();
                var url = GetFullUrl($"api/BugReports/{id}");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<BugReport>();
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting bug report {Id}", id);
                return null;
            }
        }

        public async Task AddCommentAsync(Guid bugId, string comment, string? status)
        {
            try
            {
                EnsureAuthorization();
                var currentUser = _authService.CurrentUser;
                var bugComment = new BugComment
                {
                    BugReportId = bugId,
                    Content = comment,
                    AuthorName = $"{currentUser?.FirstName} {currentUser?.LastName}".Trim(),
                    AuthorEmail = currentUser?.Email ?? "",
                    IsDevComment = _permissionService.IsDev,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var url = GetFullUrl($"api/BugReports/{bugId}/comments");
                if (!string.IsNullOrEmpty(status))
                {
                    url += $"?status={status}";
                }

                var response = await _httpClient.PostAsJsonAsync(url, bugComment);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding comment to bug {Id}", bugId);
                throw;
            }
        }

        public async Task MarkAsSolutionAsync(Guid commentId)
        {
            try
            {
                EnsureAuthorization();
                var url = GetFullUrl($"api/BugReports/comments/{commentId}/solution");
                var response = await _httpClient.PostAsync(url, null);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking comment {Id} as solution", commentId);
                throw;
            }
        }

        public async Task DeleteCommentAsync(Guid commentId)
        {
            try
            {
                EnsureAuthorization();
                var url = GetFullUrl($"api/BugReports/comments/{commentId}");
                var response = await _httpClient.DeleteAsync(url);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting comment {Id}", commentId);
                throw;
            }
        }

        public async Task DeleteBugAsync(Guid bugId)
        {
            try
            {
                EnsureAuthorization();
                var url = GetFullUrl($"api/BugReports/{bugId}");
                var response = await _httpClient.DeleteAsync(url);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting bug {Id}", bugId);
                throw;
            }
        }
    }
}
