using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Infrastructure;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiAuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private User? _currentUser;
        private string? _authToken;

        public ApiAuthService()
        {
            _httpClient = new HttpClient();
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = ConnectionSettings.Instance.ApiBaseUrl;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        public User? CurrentUser => _currentUser;
        public string? AuthToken => _authToken;
        public bool IsAuthenticated => _currentUser != null;

        public async Task<(bool Success, string ErrorMessage)> LoginAsync(string email, string password)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(GetFullUrl("api/Auth/login"), new LoginRequest { Email = email, Password = password });
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                    if (result != null)
                    {
                        _currentUser = result.User;
                        _authToken = result.Token;
                        // Add token to default headers for future requests
                        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                        return (true, string.Empty);
                    }
                }
                else
                {
                    await ApiLogging.LogFailureAsync("Login", response);

                    if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                    {
                        return (false, "The service is currently unavailable. Please try again later.");
                    }

                    // Read error message from API (e.g., "Account pending approval...")
                    var errorContent = await response.Content.ReadAsStringAsync();

                    // Check for HTML content in error to avoid displaying raw HTML to the user
                    if (!string.IsNullOrWhiteSpace(errorContent) && (errorContent.TrimStart().StartsWith("<") || response.Content.Headers.ContentType?.MediaType == "text/html"))
                    {
                        return (false, $"An unexpected error occurred. (Status: {response.StatusCode})");
                    }

                    // Clean up quotes if it's a JSON string
                    return (false, errorContent.Trim('"'));
                }
            }
            catch (Exception ex)
            {
                ApiLogging.LogException("Login", ex, GetFullUrl("api/Auth/login"));
                // Log error
                return (false, "Connection error: " + ex.Message);
            }
            return (false, "Unknown error occurred.");
        }

        public async Task<bool> RegisterAsync(User user)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(GetFullUrl("api/auth/register"), user);
                if (!response.IsSuccessStatusCode)
                {
                    await ApiLogging.LogFailureAsync("Register", response);
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                ApiLogging.LogException("Register", ex, GetFullUrl("api/auth/register"));
                return false;
            }
        }

        public async Task LogoutAsync()
        {
            try
            {
                if (IsAuthenticated)
                {
                    await _httpClient.PostAsync(GetFullUrl("api/Auth/logout"), null);
                }
            }
            catch
            {
                // Ignore network errors during logout, we just want to clear local state
            }
            finally
            {
                _currentUser = null;
                _authToken = null;
                _httpClient.DefaultRequestHeaders.Authorization = null;
            }
        }

        public async Task<bool> UpdateProfileAsync(User user)
        {
            try
            {
                if (user == null) return false;
                var response = await _httpClient.PutAsJsonAsync(GetFullUrl($"api/Users/{user.Id}"), user);
                if (response.IsSuccessStatusCode)
                {
                    _currentUser = user; // Update local cache
                    return true;
                }
                await ApiLogging.LogFailureAsync("UpdateProfile", response);
                return false;
            }
            catch (Exception ex)
            {
                ApiLogging.LogException("UpdateProfile", ex, GetFullUrl($"api/Users/{user.Id}"));
                return false;
            }
        }

        public async Task<bool> ChangePasswordAsync(string oldPassword, string newPassword)
        {
            try
            {
                var request = new OCC.Shared.DTOs.ChangePasswordRequest 
                { 
                    OldPassword = oldPassword, 
                    NewPassword = newPassword 
                };
                var response = await _httpClient.PostAsJsonAsync(GetFullUrl("api/Users/change-password"), request);
                if (!response.IsSuccessStatusCode)
                {
                    await ApiLogging.LogFailureAsync("ChangePassword", response);
                }
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                ApiLogging.LogException("ChangePassword", ex, GetFullUrl("api/Users/change-password"));
                return false;
            }
        }

        private class LoginResponse
        {
            public string Token { get; set; } = string.Empty;
            public User User { get; set; } = new();
        }
    }
}
