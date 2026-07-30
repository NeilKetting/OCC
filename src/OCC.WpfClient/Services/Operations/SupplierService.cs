using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services
{
    public class SupplierService : ISupplierService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthService _authService;
        private readonly ILogger<SupplierService> _logger;
        private readonly ConnectionSettings _connectionSettings;

        public SupplierService(IHttpClientFactory httpClientFactory, IAuthService authService, ILogger<SupplierService> logger, ConnectionSettings connectionSettings)
        {
            _httpClientFactory = httpClientFactory;
            _authService = authService;
            _logger = logger;
            _connectionSettings = connectionSettings;
        }

        private void EnsureAuthorization(HttpClient client)
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _connectionSettings.ApiBaseUrl ?? "http://localhost:5237/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }

        public async Task<IEnumerable<SupplierSummaryDto>> GetSupplierSummariesAsync()
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/Suppliers/summaries");
            try
            {
                return await client.GetFromJsonAsync<IEnumerable<SupplierSummaryDto>>(url) ?? new List<SupplierSummaryDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching supplier summaries from {Url}", url);
                throw;
            }
        }

        public async Task<IEnumerable<Supplier>> GetSuppliersAsync()
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/Suppliers");
            try
            {
                return await client.GetFromJsonAsync<IEnumerable<Supplier>>(url) ?? new List<Supplier>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching suppliers from {Url}", url);
                throw;
            }
        }

        public async Task<Supplier?> GetSupplierAsync(Guid id)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/Suppliers/{id}");
            try
            {
                return await client.GetFromJsonAsync<Supplier>(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching supplier {Id} from {Url}", id, url);
                throw;
            }
        }

        public async Task<Supplier> CreateSupplierAsync(Supplier supplier)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/Suppliers");
            try
            {
                var response = await client.PostAsJsonAsync(url, supplier);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<Supplier>() ?? supplier;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating supplier at {Url}", url);
                throw;
            }
        }

        public async Task UpdateSupplierAsync(Supplier supplier)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/Suppliers/{supplier.Id}");
            try
            {
                var response = await client.PutAsJsonAsync(url, supplier);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating supplier {Id} at {Url}", supplier.Id, url);
                throw;
            }
        }

        public async Task DeleteSupplierAsync(Guid id)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/Suppliers/{id}");
            try
            {
                var response = await client.DeleteAsync(url);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting supplier {Id} at {Url}", id, url);
                throw;
            }
        }
    }
}
