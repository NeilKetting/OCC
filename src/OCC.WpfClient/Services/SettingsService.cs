using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthService _authService;
        private readonly ILogger<SettingsService> _logger;
        private readonly ConnectionSettings _connectionSettings;
        private const string KeyName = "CompanyProfile";

        public SettingsService(IHttpClientFactory httpClientFactory, IAuthService authService, ILogger<SettingsService> logger, ConnectionSettings connectionSettings)
        {
            _httpClientFactory = httpClientFactory;
            _authService = authService;
            _logger = logger;
            _connectionSettings = connectionSettings;
        }

        private void EnsureAuthorization(HttpClient client)
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5237/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        public async Task<CompanyDetails> GetCompanyDetailsAsync()
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/AppSettings");
            
            try
            {
                var settings = await client.GetFromJsonAsync<List<AppSetting>>(url);
                var profile = settings?.FirstOrDefault(s => s.Key == KeyName);

                if (profile != null && !string.IsNullOrEmpty(profile.Value))
                {
                    var details = JsonSerializer.Deserialize<CompanyDetails>(profile.Value);
                    if (details != null) return details;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching company settings from {Url}", url);
            }

            return new CompanyDetails(); // Default
        }

        public async Task SaveCompanyDetailsAsync(CompanyDetails details)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            
            try
            {
                // check if exists first
                var getUrl = GetFullUrl("api/AppSettings");
                var settings = await client.GetFromJsonAsync<List<AppSetting>>(getUrl);
                var existing = settings?.FirstOrDefault(s => s.Key == KeyName);
                
                var json = JsonSerializer.Serialize(details);

                if (existing != null)
                {
                    existing.Value = json;
                    var putUrl = GetFullUrl($"api/AppSettings/{existing.Id}");
                    var response = await client.PutAsJsonAsync(putUrl, existing);
                    response.EnsureSuccessStatusCode();
                }
                else
                {
                    var newSetting = new AppSetting
                    {
                        Key = KeyName,
                        Value = json
                    };
                    var postUrl = GetFullUrl("api/AppSettings");
                    var response = await client.PostAsJsonAsync(postUrl, newSetting);
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving company settings");
                throw;
            }
        }
    }
}
