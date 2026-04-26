using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using OCC.Shared.Models;
using OCC.Shared.DTOs;

namespace OCC.Mobile.Services
{
    public interface IAuthService
    {
        User? CurrentUser { get; }
        string? CurrentToken { get; }
        Task<(bool Success, string ErrorMessage)> LoginAsync(string email, string password);
        void Logout();
    }

    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly ILocalSettingsService _settingsService;
        
        public User? CurrentUser { get; private set; }
        public string? CurrentToken { get; private set; }

        public AuthService(ILocalSettingsService settingsService)
        {
            _settingsService = settingsService;
            
            // Setup HttpClient
            // In a production app, we'd use IHttpClientFactory, 
            // but for a mobile app, a singleton HttpClient is often used.
            _httpClient = new HttpClient();
        }

        private string GetBaseUrl()
        {
            if (_settingsService.Settings.SelectedEnvironment == AppEnvironment.Local)
            {
                // If running on Android Emulator, localhost is 10.0.2.2
                // We detect platform or just use a sensible default.
                // For now, let's use the local IP or localhost.
                
                #if ANDROID
                return "http://10.0.2.2:5237/";
                #else
                return "http://localhost:5237/";
                #endif
            }
            
            return "http://102.221.36.149:8081/";
        }

        public async Task<(bool Success, string ErrorMessage)> LoginAsync(string email, string password)
        {
            try
            {
                var baseUrl = GetBaseUrl();
                var url = $"{baseUrl}api/Auth/login";
                
                var response = await _httpClient.PostAsJsonAsync(url, new LoginRequest 
                { 
                    Email = email, 
                    Password = password 
                });

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                    if (result != null)
                    {
                        CurrentUser = result.User;
                        CurrentToken = result.Token;
                        
                        // Set Authorization header for subsequent requests
                        _httpClient.DefaultRequestHeaders.Authorization = 
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", CurrentToken);
                        
                        return (true, string.Empty);
                    }
                }

                var error = await response.Content.ReadAsStringAsync();
                return (false, string.IsNullOrWhiteSpace(error) ? "Invalid credentials" : error.Trim('"'));
            }
            catch (Exception ex)
            {
                return (false, $"Connection error: {ex.Message}");
            }
        }

        public void Logout()
        {
            CurrentUser = null;
            CurrentToken = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        private class LoginResponse
        {
            public string Token { get; set; } = string.Empty;
            public User User { get; set; } = new();
        }
    }
}
