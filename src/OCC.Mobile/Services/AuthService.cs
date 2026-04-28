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
        Task<(bool Success, User? User, string ErrorMessage)> RegisterAsync(User user);
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

        public async Task<(bool Success, User? User, string ErrorMessage)> RegisterAsync(User user)
        {
            try
            {
                var baseUrl = GetBaseUrl();
                var url = $"{baseUrl}api/Auth/register";

                var response = await _httpClient.PostAsJsonAsync(url, user);

                if (response.IsSuccessStatusCode)
                {
                    var createdUser = await response.Content.ReadFromJsonAsync<User>();
                    return (true, createdUser, string.Empty);
                }

                var error = await response.Content.ReadAsStringAsync();
                return (false, null, string.IsNullOrWhiteSpace(error) ? "Registration failed" : error.Trim('"'));
            }
            catch (Exception ex)
            {
                return (false, null, $"Connection error: {ex.Message}");
            }
        }

        private class LoginResponse
        {
            public string Token { get; set; } = string.Empty;
            public User User { get; set; } = new();
        }
    }
}
