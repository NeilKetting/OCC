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
    public class EmployeeService : IEmployeeService
    {
        private readonly ILogger<EmployeeService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;
        private readonly JsonSerializerOptions _options;

        public EmployeeService(ILogger<EmployeeService> logger, 
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
                PropertyNameCaseInsensitive = true,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
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

        public async Task<IEnumerable<EmployeeSummaryDto>> GetEmployeesAsync()
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/Employees");
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<EmployeeSummaryDto>>(url, _options) 
                       ?? new List<EmployeeSummaryDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employees from {Url}", url);
                throw;
            }
        }

        public async Task<EmployeeDto?> GetEmployeeAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Employees/{id}");
            try
            {
                return await _httpClient.GetFromJsonAsync<EmployeeDto>(url, _options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching employee {Id} from {Url}", id, url);
                throw;
            }
        }

        public async Task<EmployeeDto?> CreateEmployeeAsync(Employee employee)
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/Employees");
            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, employee, _options);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error creating employee: {response.StatusCode}. Details: {errorContent}", null, response.StatusCode);
                }

                return await response.Content.ReadFromJsonAsync<EmployeeDto>(_options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating employee at {Url}", url);
                throw;
            }
        }

        public async Task<bool> UpdateEmployeeAsync(Employee employee)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Employees/{employee.Id}");
            try
            {
                var response = await _httpClient.PutAsJsonAsync(url, employee, _options);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    throw new ConcurrencyException("Another user has modified this employee record.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error updating employee: {response.StatusCode}. Details: {errorContent}", null, response.StatusCode);
                }

                return true;
            }
            catch (ConcurrencyException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating employee {Id} at {Url}", employee.Id, url);
                throw;
            }
        }

        public async Task<bool> DeleteEmployeeAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Employees/{id}");
            try
            {
                var response = await _httpClient.DeleteAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Error deleting employee: {response.StatusCode}. Details: {errorContent}", null, response.StatusCode);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting employee {Id} at {Url}", id, url);
                throw;
            }
        }
    }
}
