using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services
{
    public class EmployeeLoanService : IEmployeeLoanService
    {
        private readonly ILogger<EmployeeLoanService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;
        private readonly JsonSerializerOptions _options;

        public EmployeeLoanService(
            ILogger<EmployeeLoanService> logger,
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
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5000/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        public async Task<IEnumerable<EmployeeLoan>> GetAllAsync()
        {
            EnsureAuthorization();
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<EmployeeLoan>>(
                    GetFullUrl("api/EmployeeLoans"), _options) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching all loans");
                return [];
            }
        }

        public async Task<IEnumerable<EmployeeLoan>> GetActiveLoansAsync()
        {
            EnsureAuthorization();
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<EmployeeLoan>>(
                    GetFullUrl("api/EmployeeLoans?activeOnly=true"), _options) ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching active loans");
                return [];
            }
        }

        public async Task<EmployeeLoan?> GetByIdAsync(Guid id)
        {
            EnsureAuthorization();
            try
            {
                return await _httpClient.GetFromJsonAsync<EmployeeLoan>(
                    GetFullUrl($"api/EmployeeLoans/{id}"), _options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching loan {Id}", id);
                return null;
            }
        }

        public async Task<EmployeeLoan> AddAsync(EmployeeLoan loan)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync(GetFullUrl("api/EmployeeLoans"), loan, _options);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
            return await response.Content.ReadFromJsonAsync<EmployeeLoan>(_options)
                ?? throw new Exception("Failed to deserialize loan response.");
        }

        public async Task UpdateAsync(EmployeeLoan loan)
        {
            EnsureAuthorization();
            var response = await _httpClient.PutAsJsonAsync(GetFullUrl($"api/EmployeeLoans/{loan.Id}"), loan, _options);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task DeleteAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync(GetFullUrl($"api/EmployeeLoans/{id}"));
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new Exception(error);
            }
        }

        public async Task<OCC.Shared.DTOs.LoanStatementDto?> GetStatementAsync(Guid loanId)
        {
            EnsureAuthorization();
            try
            {
                return await _httpClient.GetFromJsonAsync<OCC.Shared.DTOs.LoanStatementDto>(
                    GetFullUrl($"api/EmployeeLoans/{loanId}/statement"), _options);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching statement for loan {LoanId}", loanId);
                return null;
            }
        }
    }
}
