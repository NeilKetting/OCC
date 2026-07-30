using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure.Exceptions;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Services
{
    public class UserService : IUserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;
        private readonly JsonSerializerOptions _options;

        public UserService(ILogger<UserService> logger, 
                            IHttpClientFactory httpClientFactory,
                            ConnectionSettings connectionSettings,
                            IAuthService authService)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _connectionSettings = connectionSettings;
            _authService = authService;
            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5000/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        public async Task<IEnumerable<User>> GetUsersAsync()
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/Users");
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<User>>(url, _options) 
                       ?? new List<User>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching users from {Url}", url);
                return new List<User>();
            }
        }

        public async Task<User?> GetUserAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Users/{id}");
            try
            {
                return await _httpClient.GetFromJsonAsync<User>(url, _options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching user {Id} from {Url}", id, url);
                return null;
            }
        }

        public async Task<bool> CreateUserAsync(User user)
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/Users");
            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, user, _options);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user at {Url}", url);
                return false;
            }
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Users/{user.Id}");
            try
            {
                var response = await _httpClient.PutAsJsonAsync(url, user, _options);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    throw new ConcurrencyException("Another user has modified this user record.");
                }

                return response.IsSuccessStatusCode;
            }
            catch (ConcurrencyException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user at {Url}", url);
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Users/{id}");
            try
            {
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user at {Url}", url);
                return false;
            }
        }
    }
}
