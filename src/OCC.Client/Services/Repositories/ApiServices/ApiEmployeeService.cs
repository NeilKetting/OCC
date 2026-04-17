using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System.Text.Json;

namespace OCC.Client.Services.Repositories.ApiServices
{
    public class ApiEmployeeService : IEmployeeService
    {
        private readonly HttpClient _httpClient; 
        private readonly JsonSerializerOptions _options;
        private readonly IAuthService _authService;

        public ApiEmployeeService(HttpClient httpClient, IAuthService authService)
        {
            _httpClient = httpClient;
            _authService = authService;
            _options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            };
        }
        
        private string GetFullUrl(string path)
        {
            var baseUrl = ConnectionSettings.Instance.ApiBaseUrl;
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        private void EnsureAuthorization()
        {
            var token = _authService.AuthToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<IEnumerable<EmployeeSummaryDto>> GetEmployeesAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<EmployeeSummaryDto>>(GetFullUrl("api/Employees"), _options) ?? new List<EmployeeSummaryDto>();
        }

        public async Task<EmployeeDto?> GetEmployeeAsync(Guid id)
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<EmployeeDto>(GetFullUrl($"api/Employees/{id}"), _options);
        }

        public async Task<EmployeeDto?> CreateEmployeeAsync(Employee employee)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync(GetFullUrl("api/Employees"), employee, _options);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Details: {errorContent}", null, response.StatusCode);
            }

            return await response.Content.ReadFromJsonAsync<EmployeeDto>(_options);
        }

        public async Task<bool> UpdateEmployeeAsync(Employee employee)
        {
            EnsureAuthorization();
            var response = await _httpClient.PutAsJsonAsync(GetFullUrl($"api/Employees/{employee.Id}"), employee, _options);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Details: {errorContent}", null, response.StatusCode);
            }

            return true;
        }

        public async Task<bool> DeleteEmployeeAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync(GetFullUrl($"api/Employees/{id}"));
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Details: {errorContent}", null, response.StatusCode);
            }

            return true;
        }
    }
}
