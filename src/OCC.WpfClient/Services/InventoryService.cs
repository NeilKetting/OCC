using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthService _authService;
        private readonly ILogger<InventoryService> _logger;
        private readonly ConnectionSettings _connectionSettings;

        public InventoryService(IHttpClientFactory httpClientFactory, IAuthService authService, ILogger<InventoryService> logger, ConnectionSettings connectionSettings)
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

        public async Task<IEnumerable<InventoryItem>> GetInventoryAsync()
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/Inventory");
            try
            {
                _logger.LogInformation("Fetching inventory from {Url}", url);
                return await client.GetFromJsonAsync<IEnumerable<InventoryItem>>(url) ?? new List<InventoryItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching inventory from {Url}", url);
                throw;
            }
        }

        public async Task<InventoryItem?> GetInventoryItemAsync(Guid id)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/Inventory/{id}");
            try
            {
                return await client.GetFromJsonAsync<InventoryItem>(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching inventory item {Id} from {Url}", id, url);
                throw;
            }
        }

        public async Task<InventoryItem> CreateItemAsync(InventoryItem item)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/Inventory");
            try
            {
                var response = await client.PostAsJsonAsync(url, item);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<InventoryItem>() ?? item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating inventory item at {Url}", url);
                throw;
            }
        }

        public async Task UpdateItemAsync(InventoryItem item)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/Inventory/{item.Id}");
            try
            {
                var response = await client.PutAsJsonAsync(url, item);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating inventory item {Id} at {Url}", item.Id, url);
                throw;
            }
        }

        public async Task DeleteItemAsync(Guid id)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/Inventory/{id}");
            try
            {
                var response = await client.DeleteAsync(url);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting inventory item {Id} at {Url}", id, url);
                throw;
            }
        }
    }
}
