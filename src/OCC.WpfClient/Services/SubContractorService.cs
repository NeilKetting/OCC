using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
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
    public class SubContractorService : ISubContractorService
    {
        private readonly ILogger<SubContractorService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;

        public SubContractorService(ILogger<SubContractorService> logger,
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

        public async Task<IEnumerable<SubContractorSummaryDto>> GetSubContractorSummariesAsync()
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/SubContractors/summaries");
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<SubContractorSummaryDto>>(url) ?? new List<SubContractorSummaryDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching sub-contractor summaries from {Url}", url);
                return new List<SubContractorSummaryDto>();
            }
        }

        public async Task<SubContractor?> GetSubContractorAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/SubContractors/{id}");
            try
            {
                return await _httpClient.GetFromJsonAsync<SubContractor>(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching sub-contractor {Id} from {Url}", id, url);
                throw;
            }
        }

        public async Task<SubContractor> CreateSubContractorAsync(SubContractor subContractor)
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/SubContractors");
            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, subContractor);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<SubContractor>() ?? subContractor;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating sub-contractor at {Url}", url);
                throw;
            }
        }

        public async Task<bool> UpdateSubContractorAsync(SubContractor subContractor)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/SubContractors/{subContractor.Id}");
            try
            {
                var response = await _httpClient.PutAsJsonAsync(url, subContractor);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    throw new ConcurrencyException("Another user has modified this record.");
                }

                return response.IsSuccessStatusCode;
            }
            catch (ConcurrencyException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating sub-contractor {Id} at {Url}", subContractor.Id, url);
                throw;
            }
        }

        public async Task<bool> DeleteSubContractorAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/SubContractors/{id}");
            try
            {
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting sub-contractor {Id} at {Url}", id, url);
                throw;
            }
        }
    }
}
