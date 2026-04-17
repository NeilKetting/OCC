using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using OCC.Client.Services.Interfaces;
using OCC.Shared.Models;

namespace OCC.Client.Services
{
    public class ProjectVariationOrderService : IProjectVariationOrderService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;

        public ProjectVariationOrderService(HttpClient httpClient, IAuthService authService)
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

        public async Task<IEnumerable<ProjectVariationOrder>> GetVariationOrdersAsync(Guid? projectId = null)
        {
            EnsureAuthorization();
            var url = "api/ProjectVariationOrders";
            if (projectId.HasValue)
            {
                url += $"?projectId={projectId.Value}";
            }
            return await _httpClient.GetFromJsonAsync<IEnumerable<ProjectVariationOrder>>(url) ?? new List<ProjectVariationOrder>();
        }

        public async Task<ProjectVariationOrder> GetVariationOrderAsync(Guid id)
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<ProjectVariationOrder>($"api/ProjectVariationOrders/{id}") ?? throw new Exception("Variation order not found");
        }

        public async Task<ProjectVariationOrder> CreateVariationOrderAsync(ProjectVariationOrder variationOrder)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync("api/ProjectVariationOrders", variationOrder);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProjectVariationOrder>() ?? throw new Exception("Failed to deserialize created variation order");
        }

        public async Task UpdateVariationOrderAsync(ProjectVariationOrder variationOrder)
        {
            EnsureAuthorization();
            var response = await _httpClient.PutAsJsonAsync($"api/ProjectVariationOrders/{variationOrder.Id}", variationOrder);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteVariationOrderAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync($"api/ProjectVariationOrders/{id}");
            response.EnsureSuccessStatusCode();
        }
    }
}
