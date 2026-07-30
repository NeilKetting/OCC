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

        public async Task<string?> GetGoogleMapsKeyAsync()
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/Config/google-maps-key");

            try
            {
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (data.TryGetProperty("key", out var keyProp))
                    {
                        return keyProp.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Google Maps API key");
            }

            return null;
        }

        public async Task<decimal> GetBibcRateAsync()
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/AppSettings");
            try
            {
                var settings = await client.GetFromJsonAsync<List<AppSetting>>(url);
                var setting = settings?.FirstOrDefault(s => s.Key == "BibcRate");
                if (setting != null && decimal.TryParse(setting.Value, out var rate))
                {
                    return rate;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching BIBC rate");
            }
            return 28.75m; // Default
        }

        public async Task SaveBibcRateAsync(decimal rate)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            try
            {
                var getUrl = GetFullUrl("api/AppSettings");
                var settings = await client.GetFromJsonAsync<List<AppSetting>>(getUrl);
                var existing = settings?.FirstOrDefault(s => s.Key == "BibcRate");
                var valueString = rate.ToString();

                if (existing != null)
                {
                    existing.Value = valueString;
                    var putUrl = GetFullUrl($"api/AppSettings/{existing.Id}");
                    var response = await client.PutAsJsonAsync(putUrl, existing);
                    response.EnsureSuccessStatusCode();
                }
                else
                {
                    var newSetting = new AppSetting
                    {
                        Key = "BibcRate",
                        Value = valueString
                    };
                    var postUrl = GetFullUrl("api/AppSettings");
                    var response = await client.PostAsJsonAsync(postUrl, newSetting);
                    response.EnsureSuccessStatusCode();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving BIBC rate");
                throw;
            }
        }
    }
}
