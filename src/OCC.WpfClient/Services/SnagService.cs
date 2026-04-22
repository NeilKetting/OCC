using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure.Exceptions;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services
{
    public class SnagService : ISnagService
    {
        private readonly ILogger<SnagService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;

        public SnagService(ILogger<SnagService> logger,
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

        public async Task<IEnumerable<SnagJob>> GetSnagJobsAsync()
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/SnagJobs");
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<SnagJob>>(url) ?? new List<SnagJob>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching snag jobs from {Url}", url);
                return new List<SnagJob>();
            }
        }

        public async Task<IEnumerable<SnagJob>> GetProjectSnagJobsAsync(Guid projectId)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/SnagJobs/project/{projectId}");
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<SnagJob>>(url) ?? new List<SnagJob>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching snag jobs for project {ProjectId} from {Url}", projectId, url);
                return new List<SnagJob>();
            }
        }

        public async Task<IEnumerable<SnagJob>> GetSubContractorSnagJobsAsync(Guid subContractorId)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/SnagJobs/subcontractor/{subContractorId}");
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<SnagJob>>(url) ?? new List<SnagJob>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching snag jobs for sub-contractor {SubContractorId} from {Url}", subContractorId, url);
                return new List<SnagJob>();
            }
        }

        public async Task<SnagJob?> GetSnagJobAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/SnagJobs/{id}");
            try
            {
                return await _httpClient.GetFromJsonAsync<SnagJob>(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching snag job {Id} from {Url}", id, url);
                throw;
            }
        }

        public async Task<SnagJob> CreateSnagJobAsync(SnagJob snagJob)
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/SnagJobs");
            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, snagJob);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<SnagJob>() ?? snagJob;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating snag job at {Url}", url);
                throw;
            }
        }

        public async Task<bool> UpdateSnagJobAsync(SnagJob snagJob)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/SnagJobs/{snagJob.Id}");
            try
            {
                var response = await _httpClient.PutAsJsonAsync(url, snagJob);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    throw new ConcurrencyException("Another user has modified this record.");
                }

                return response.IsSuccessStatusCode;
            }
            catch (ConcurrencyException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating snag job {Id} at {Url}", snagJob.Id, url);
                throw;
            }
        }

        public async Task<bool> DeleteSnagJobAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/SnagJobs/{id}");
            try
            {
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting snag job {Id} at {Url}", id, url);
                throw;
            }
        }
    }
}
