using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Shared.Models;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.Client.Services
{
    public class SettingsService : ISettingsService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;
        private const string KeyName = "CompanyProfile";

        public SettingsService(HttpClient httpClient, IAuthService authService)
        {
            _httpClient = httpClient;
            _authService = authService;
        }

        private void EnsureAuthorization()
        {
            var token = _authService.AuthToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<CompanyDetails> GetCompanyDetailsAsync()
        {
            try
            {
                EnsureAuthorization();
                var settings = await _httpClient.GetFromJsonAsync<List<AppSetting>>("api/AppSettings");
                var profile = settings?.FirstOrDefault(s => s.Key == KeyName);

                if (profile != null && !string.IsNullOrEmpty(profile.Value))
                {
                    var details = JsonSerializer.Deserialize<CompanyDetails>(profile.Value);
                    if (details != null) return details;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error fetching company settings: {ex.Message}");
            }

            return new CompanyDetails(); // Default
        }

        public async Task SaveCompanyDetailsAsync(CompanyDetails details)
        {
            try
            {
                EnsureAuthorization();
                // check if exists first
                var settings = await _httpClient.GetFromJsonAsync<List<AppSetting>>("api/AppSettings");
                var existing = settings?.FirstOrDefault(s => s.Key == KeyName);
                
                var json = JsonSerializer.Serialize(details);

                if (existing != null)
                {
                    existing.Value = json;
                    await _httpClient.PutAsJsonAsync($"api/AppSettings/{existing.Id}", existing);
                }
                else
                {
                    var newSetting = new AppSetting
                    {
                        Key = KeyName,
                        Value = json
                    };
                    await _httpClient.PostAsJsonAsync("api/AppSettings", newSetting);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving company settings: {ex.Message}");
                throw;
            }
        }
    }
}
