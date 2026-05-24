using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services
{
    public class ProjectReportService : IProjectReportService
    {
        private readonly ILogger<ProjectReportService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;

        public ProjectReportService(ILogger<ProjectReportService> logger,
                                    IHttpClientFactory httpClientFactory,
                                    ConnectionSettings connectionSettings,
                                    IAuthService authService)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _connectionSettings = connectionSettings;
            _authService = authService;
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5000/";
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

        public async Task<ProjectReportDraft?> GetDraftAsync(Guid projectId)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/ProjectReports/draft/{projectId}");
            try
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ProjectReportDraft>();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching project report draft from {Url}", url);
                return null;
            }
        }

        public async Task<bool> SaveDraftAsync(Guid projectId, ProjectReportDraft draft)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/ProjectReports/draft/{projectId}");
            try
            {
                var response = await _httpClient.PutAsJsonAsync(url, draft);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving project report draft to {Url}", url);
                return false;
            }
        }

        public async Task<IEnumerable<ProjectReportHistory>> GetHistoryAsync(Guid projectId)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/ProjectReports/history/{projectId}");
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<ProjectReportHistory>>(url) ?? new List<ProjectReportHistory>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching project report history from {Url}", url);
                return new List<ProjectReportHistory>();
            }
        }

        public async Task<ProjectReportHistory?> UploadReportAsync(Guid projectId, int weekNumber, string reportName, Stream fileStream, string fileName)
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/ProjectReports/history");
            try
            {
                using var content = new MultipartFormDataContent();
                
                content.Add(new StringContent(projectId.ToString()), "projectId");
                content.Add(new StringContent(reportName), "reportName");
                content.Add(new StringContent(weekNumber.ToString()), "weekNumber");
                
                var fileContent = new StreamContent(fileStream);
                fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                content.Add(fileContent, "file", fileName);

                var response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ProjectReportHistory>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading project report PDF to {Url}", url);
                return null;
            }
        }
    }
}
