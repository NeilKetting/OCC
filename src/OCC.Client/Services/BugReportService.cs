using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Infrastructure;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace OCC.Client.Services
{
    public class BugReportService : IBugReportService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;
        private readonly IPermissionService _permissionService;
        private readonly Microsoft.Extensions.Logging.ILogger<BugReportService> _logger;
        
        public BugReportService(HttpClient httpClient, IAuthService authService, IPermissionService permissionService, Microsoft.Extensions.Logging.ILogger<BugReportService> logger)
        {
            _httpClient = httpClient;
            _authService = authService;
            _permissionService = permissionService;
            _logger = logger;
        }

        private void EnsureAuthorization()
        {
            var token = _authService.AuthToken;
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
                var response = await _httpClient.PostAsJsonAsync("api/BugReports", report);
                if (!response.IsSuccessStatusCode)
                {
                    await ApiLogging.LogFailureAsync("SubmitBug", response);
                }
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                ApiLogging.LogException("SubmitBug", ex);
                throw;
            }
        }

        public async Task<IEnumerable<BugReport>> GetBugReportsAsync(bool includeArchived = false)
        {
            var url = $"api/BugReports?includeArchived={includeArchived}";
            try
            {
                EnsureAuthorization();
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<BugReport>>() ?? new List<BugReport>();
                }

                await ApiLogging.LogFailureAsync("GetBugReports", response);
                return new List<BugReport>();
            }
            catch (Exception ex)
            {
                ApiLogging.LogException("GetBugReports", ex, url);
                return new List<BugReport>();
            }
        }

        public async Task<IEnumerable<BugReport>> SearchSolutionsAsync(string query)
        {
            try
            {
                var response = await _httpClient.GetAsync($"api/BugReports/solutions?q={Uri.EscapeDataString(query)}");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<IEnumerable<BugReport>>() ?? new List<BugReport>();
            }
            catch (Exception ex)
            {
                ApiLogging.LogException("SearchSolutions", ex, $"api/BugReports/solutions?q={Uri.EscapeDataString(query)}");
                _logger.LogError(ex, "Error searching solutions");
                return new List<BugReport>();
            }
        }

        public async Task<BugReport?> GetBugReportAsync(Guid id)
        {
            EnsureAuthorization();
            try 
            {
                EnsureAuthorization();
                var response = await _httpClient.GetAsync($"api/BugReports/{id}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<BugReport>();
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;

                await ApiLogging.LogFailureAsync("GetBugReport", response);
                return null;
            }
            catch (Exception ex)
            {
                ApiLogging.LogException("GetBugReport", ex, $"api/BugReports/{id}");
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
                    AuthorName = (currentUser?.FirstName + " " + currentUser?.LastName)?.Trim() ?? "System",
                    AuthorEmail = currentUser?.Email ?? "",
                    IsDevComment = _permissionService.IsDev,
                    CreatedAtUtc = DateTime.UtcNow
                };

                var url = $"api/BugReports/{bugId}/comments";
                if (!string.IsNullOrEmpty(status))
                {
                    url += $"?status={status}";
                }

                var response = await _httpClient.PostAsJsonAsync(url, bugComment);
                if (!response.IsSuccessStatusCode)
                {
                    await ApiLogging.LogFailureAsync("AddComment", response);
                }
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                ApiLogging.LogException("AddComment", ex);
                throw;
            }
        }

        public async Task DeleteCommentAsync(Guid commentId)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync($"api/BugReports/comments/{commentId}");
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteBugAsync(Guid bugId)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync($"api/BugReports/{bugId}");
            response.EnsureSuccessStatusCode();
        }
    }
}
