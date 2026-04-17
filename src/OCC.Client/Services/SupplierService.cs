using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OCC.Client.Services
{
    public class SupplierService : ISupplierService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;

        public SupplierService(HttpClient httpClient, IAuthService authService)
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

        public async Task<IEnumerable<SupplierSummaryDto>> GetSupplierSummariesAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<SupplierSummaryDto>>("api/Suppliers/summaries") ?? new List<SupplierSummaryDto>();
        }

        public async Task<IEnumerable<Supplier>> GetSuppliersAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<Supplier>>("api/Suppliers") ?? new List<Supplier>();
        }

        public async Task<Supplier?> GetSupplierAsync(Guid id)
        {
             EnsureAuthorization();
             return await _httpClient.GetFromJsonAsync<Supplier>($"api/Suppliers/{id}");
        }

        public async Task<Supplier> CreateSupplierAsync(Supplier supplier)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync("api/Suppliers", supplier);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to create supplier: {response.StatusCode} - {error}");
            }

            return await response.Content.ReadFromJsonAsync<Supplier>() ?? supplier;
        }

        public async Task UpdateSupplierAsync(Supplier supplier)
        {
             EnsureAuthorization();
             var response = await _httpClient.PutAsJsonAsync($"api/Suppliers/{supplier.Id}", supplier);
             
             if (!response.IsSuccessStatusCode)
             {
                 var error = await response.Content.ReadAsStringAsync();
                 throw new HttpRequestException($"Failed to update supplier: {response.StatusCode} - {error}");
             }
        }

        public async Task DeleteSupplierAsync(Guid id)
        {
             EnsureAuthorization();
             var response = await _httpClient.DeleteAsync($"api/Suppliers/{id}");
             response.EnsureSuccessStatusCode();
        }
    }
}
