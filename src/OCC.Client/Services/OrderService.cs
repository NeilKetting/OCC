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
using System.Linq;

namespace OCC.Client.Services
{
    public class OrderService : IOrderService
    {
        private readonly HttpClient _httpClient;
        private readonly IInventoryService _inventoryService;
        private readonly IAuthService _authService;

        public OrderService(HttpClient httpClient, IInventoryService inventoryService, IAuthService authService)
        {
            _httpClient = httpClient;
            _inventoryService = inventoryService;
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

        public async Task<List<OrderSummaryDto>> GetOrdersAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<List<OrderSummaryDto>>("api/Orders") ?? new List<OrderSummaryDto>();
        }

        public async Task<OrderDto?> GetOrderAsync(Guid id)
        {
             EnsureAuthorization();
             return await _httpClient.GetFromJsonAsync<OrderDto>($"api/Orders/{id}");
        }

        public async Task<OrderDto> CreateOrderAsync(OrderDto order)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync("api/Orders", order);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"Failed to create order: {response.StatusCode} - {error}");
            }
            
            return await response.Content.ReadFromJsonAsync<OrderDto>() ?? order;
        }

        public async Task UpdateOrderAsync(OrderDto order)
        {
             EnsureAuthorization();
             var response = await _httpClient.PutAsJsonAsync($"api/Orders/{order.Id}", order);
             
             if (!response.IsSuccessStatusCode)
             {
                 var error = await response.Content.ReadAsStringAsync();
                 throw new HttpRequestException($"Failed to update order: {response.StatusCode} - {error}");
             }
        }

        public async Task<OrderDto?> ReceiveOrderAsync(Guid orderId, List<OrderLineDto> updatedLines)
        {
             EnsureAuthorization();
             var response = await _httpClient.PostAsJsonAsync($"api/Orders/{orderId}/receive", updatedLines);
             response.EnsureSuccessStatusCode();

             return await response.Content.ReadFromJsonAsync<OrderDto>();
        }

        public async Task DeleteOrderAsync(Guid id)
        {
             var response = await _httpClient.DeleteAsync($"api/Orders/{id}");
             response.EnsureSuccessStatusCode();
        }

        public async Task<OrderDto> GetRestockTemplateAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<OrderDto>("api/Orders/restock-template") 
                   ?? new OrderDto();
        }

        public async Task<IEnumerable<RestockCandidateDto>> GetRestockCandidatesAsync(Branch? branch = null)
        {
            EnsureAuthorization();
            var query = branch.HasValue ? $"?branch={branch}" : "";
            return await _httpClient.GetFromJsonAsync<IEnumerable<RestockCandidateDto>>($"api/Orders/restock-candidates{query}") 
                   ?? new List<RestockCandidateDto>();
        }
    }
}
