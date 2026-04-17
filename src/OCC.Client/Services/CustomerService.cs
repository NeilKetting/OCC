using OCC.Client.Services.Interfaces;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OCC.Client.Services
{
    public class CustomerService : ICustomerService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;

        public CustomerService(HttpClient httpClient, IAuthService authService)
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

        public async Task<IEnumerable<CustomerSummaryDto>> GetCustomerSummariesAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<CustomerSummaryDto>>("api/Customers/summaries") ?? new List<CustomerSummaryDto>();
        }

        public async Task<Customer?> GetCustomerAsync(Guid id)
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<Customer>($"api/Customers/{id}");
        }

        public async Task<Customer> CreateCustomerAsync(Customer customer)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync("api/Customers", customer);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Customer>() ?? customer;
        }

        public async Task<bool> UpdateCustomerAsync(Customer customer)
        {
            EnsureAuthorization();
            var response = await _httpClient.PutAsJsonAsync($"api/Customers/{customer.Id}", customer);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteCustomerAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync($"api/Customers/{id}");
            return response.IsSuccessStatusCode;
        }
    }
}
