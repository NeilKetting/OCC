using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using OCC.Shared.DTOs;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Services
{
    public class CrewDeploymentService : ICrewDeploymentService
    {
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _settings;
        private readonly IAuthService _authService;
        private readonly JsonSerializerOptions _options;

        public CrewDeploymentService(HttpClient httpClient, ConnectionSettings settings, IAuthService authService)
        {
            _httpClient = httpClient;
            _settings = settings;
            _authService = authService;
            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            };
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _settings.ApiBaseUrl;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        public async Task<IEnumerable<SiteDeploymentDto>> GetDeploymentsAsync(Guid? projectId = null, DateTime? date = null)
        {
            EnsureAuthorization();
            var url = "api/SiteDeployments";
            var queryParts = new List<string>();
            if (projectId.HasValue) queryParts.Add($"projectId={projectId.Value}");
            if (date.HasValue) queryParts.Add($"date={date.Value:yyyy-MM-dd}");
            if (queryParts.Count > 0) url += "?" + string.Join("&", queryParts);

            return await _httpClient.GetFromJsonAsync<IEnumerable<SiteDeploymentDto>>(GetFullUrl(url), _options)
                   ?? new List<SiteDeploymentDto>();
        }

        public async Task<SiteDeploymentDto?> CreateDeploymentAsync(CreateSiteDeploymentRequest request)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync(GetFullUrl("api/SiteDeployments"), request, _options);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<SiteDeploymentDto>(_options);
            return null;
        }

        public async Task<bool> CancelDeploymentAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync(GetFullUrl($"api/SiteDeployments/{id}"));
            return response.IsSuccessStatusCode;
        }

        public async Task<IEnumerable<EmployeeSummaryDto>> GetTodayClockedInAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<EmployeeSummaryDto>>(
                GetFullUrl("api/SiteDeployments/today-clocked-in"), _options)
                ?? new List<EmployeeSummaryDto>();
        }
    }
}
