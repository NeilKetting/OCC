using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using OCC.Shared.DTOs;

namespace OCC.Mobile.Services
{
    public class SiteDeploymentService : ISiteDeploymentService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalSettingsService _settingsService;
        private readonly IAuthService _authService;
        private readonly JsonSerializerOptions _options;

        public SiteDeploymentService(ILocalSettingsService settingsService, IAuthService authService)
        {
            _settingsService = settingsService;
            _authService = authService;
            _httpClient = new HttpClient();
            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            };
        }

        private string GetBaseUrl()
        {
            if (_settingsService.Settings.SelectedEnvironment.IsLocal())
            {
                if (!string.IsNullOrEmpty(_settingsService.Settings.CustomLocalUrl))
                {
                    var url = _settingsService.Settings.CustomLocalUrl.Trim();
                    if (!url.EndsWith("/")) url += "/";
                    return url;
                }

                #if ANDROID
                return "http://10.0.2.2:5237/";
                #else
                return "http://localhost:5237/";
                #endif
            }
            return "https://api.origize63.co.za/";
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<IEnumerable<SiteDeploymentDto>> GetPendingDeploymentsAsync(Guid siteManagerId, DateTime? date = null)
        {
            try
            {
                EnsureAuthorization();
                var baseUrl = GetBaseUrl();
                var d = (date ?? DateTime.Today).ToString("yyyy-MM-dd");
                var url = $"{baseUrl}api/SiteDeployments?siteManagerId={siteManagerId}&date={d}&status=Pending";
                return await _httpClient.GetFromJsonAsync<List<SiteDeploymentDto>>(url, _options)
                       ?? new List<SiteDeploymentDto>();
            }
            catch
            {
                return new List<SiteDeploymentDto>();
            }
        }

        public async Task<bool> ReceiveDeploymentAsync(Guid deploymentId, ReceiveDeploymentRequest request)
        {
            try
            {
                EnsureAuthorization();
                var baseUrl = GetBaseUrl();
                var url = $"{baseUrl}api/SiteDeployments/{deploymentId}/receive";
                var response = await _httpClient.PostAsJsonAsync(url, request, _options);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}
