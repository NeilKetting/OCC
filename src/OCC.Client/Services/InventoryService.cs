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
    public class InventoryService : IInventoryService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;

        public InventoryService(HttpClient httpClient, IAuthService authService)
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

        public async Task<IEnumerable<InventorySummaryDto>> GetInventorySummariesAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<InventorySummaryDto>>("api/Inventory/summaries") ?? new List<InventorySummaryDto>();
        }

        public async Task<IEnumerable<InventoryItem>> GetInventoryAsync()
        {
            EnsureAuthorization();
            EnsureAuthorization();
            using var response = await _httpClient.GetAsync("api/Inventory");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to get inventory: {response.StatusCode} - {error}");
            }
            return await response.Content.ReadFromJsonAsync<IEnumerable<InventoryItem>>() ?? new List<InventoryItem>();
        }

        public async Task<InventoryItem?> GetInventoryItemAsync(Guid id)
        {
             EnsureAuthorization();
             return await _httpClient.GetFromJsonAsync<InventoryItem>($"api/Inventory/{id}");
        }

        public async Task<InventoryItem> CreateItemAsync(InventoryItem item)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync("api/Inventory", item);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                // If the error looks like it might be JSON (e.g. Problem Details), we could try to parse it, 
                // but for now, raw string is better than nothing.
                throw new HttpRequestException($"Failed to create item: {response.StatusCode} - {error}");
            }

            return await response.Content.ReadFromJsonAsync<InventoryItem>() ?? item;
        }

        public async Task UpdateItemAsync(InventoryItem item)
        {
             EnsureAuthorization();
             var response = await _httpClient.PutAsJsonAsync($"api/Inventory/{item.Id}", item);
             
             if (!response.IsSuccessStatusCode)
             {
                 var error = await response.Content.ReadAsStringAsync();
                 throw new HttpRequestException($"Failed to update item: {response.StatusCode} - {error}");
             }
        }
        public async Task DeleteItemAsync(Guid id)
        {
             EnsureAuthorization();
             var response = await _httpClient.DeleteAsync($"api/Inventory/{id}");
             if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
             {
                 var msg = await response.Content.ReadAsStringAsync();
                 throw new InvalidOperationException(msg); // Custom exception for logic handling
             }
             response.EnsureSuccessStatusCode();
        }
    }
}
