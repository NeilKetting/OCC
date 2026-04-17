using OCC.WpfClient.Services.Interfaces;
using OCC.WpfClient.Services.Infrastructure;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure.Exceptions;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly ILogger<CustomerService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;

        public CustomerService(ILogger<CustomerService> logger,
                               IHttpClientFactory httpClientFactory,
                               ConnectionSettings connectionSettings,
                               IAuthService authService)
        {
            _logger = logger;
            _httpClient = httpClientFactory.CreateClient();
            _connectionSettings = connectionSettings;
            _authService = authService;
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5000/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<IEnumerable<CustomerSummaryDto>> GetCustomerSummariesAsync()
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/Customers/summaries");
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<CustomerSummaryDto>>(url) ?? new List<CustomerSummaryDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching customer summaries from {Url}", url);
                return new List<CustomerSummaryDto>();
            }
        }

        public async Task<Customer?> GetCustomerAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Customers/{id}");
            try
            {
                return await _httpClient.GetFromJsonAsync<Customer>(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching customer {Id} from {Url}", id, url);
                throw;
            }
        }

        public async Task<Customer> CreateCustomerAsync(Customer customer)
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/Customers");
            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, customer);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<Customer>() ?? customer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer at {Url}", url);
                throw;
            }
        }

        public async Task<bool> UpdateCustomerAsync(Customer customer)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Customers/{customer.Id}");
            try
            {
                var response = await _httpClient.PutAsJsonAsync(url, customer);
                
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    throw new ConcurrencyException("Another user has modified this record.");
                }

                return response.IsSuccessStatusCode;
            }
            catch (ConcurrencyException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer {Id} at {Url}", customer.Id, url);
                throw;
            }
        }

        public async Task<bool> DeleteCustomerAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/Customers/{id}");
            try
            {
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer {Id} at {Url}", id, url);
                throw;
            }
        }
    }
}
