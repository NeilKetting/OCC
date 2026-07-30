using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.WpfClient.Infrastructure.Exceptions;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services
{
    public class ProjectVariationOrderService : IProjectVariationOrderService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthService _authService;
        private readonly ILogger<ProjectVariationOrderService> _logger;
        private readonly ConnectionSettings _connectionSettings;

        public ProjectVariationOrderService(
            IHttpClientFactory httpClientFactory, 
            IAuthService authService, 
            ILogger<ProjectVariationOrderService> logger, 
            ConnectionSettings connectionSettings)
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

        public async Task<IEnumerable<ProjectVariationOrder>> GetVariationOrdersAsync(Guid? projectId = null)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = "api/ProjectVariationOrders";
            if (projectId.HasValue)
            {
                url += $"?projectId={projectId.Value}";
            }
            var fullUrl = GetFullUrl(url);
            try
            {
                return await client.GetFromJsonAsync<IEnumerable<ProjectVariationOrder>>(fullUrl) ?? new List<ProjectVariationOrder>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching variation orders from {Url}", fullUrl);
                throw;
            }
        }

        public async Task<ProjectVariationOrder> GetVariationOrderAsync(Guid id)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var fullUrl = GetFullUrl($"api/ProjectVariationOrders/{id}");
            try
            {
                return await client.GetFromJsonAsync<ProjectVariationOrder>(fullUrl) ?? throw new Exception("Variation order not found");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching variation order {Id} from {Url}", id, fullUrl);
                throw;
            }
        }

        public async Task<ProjectVariationOrder> CreateVariationOrderAsync(ProjectVariationOrder variationOrder)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var fullUrl = GetFullUrl("api/ProjectVariationOrders");
            try
            {
                var response = await client.PostAsJsonAsync(fullUrl, variationOrder);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ProjectVariationOrder>() ?? throw new Exception("Failed to deserialize created variation order");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating variation order at {Url}", fullUrl);
                throw;
            }
        }

        public async Task UpdateVariationOrderAsync(ProjectVariationOrder variationOrder)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var fullUrl = GetFullUrl($"api/ProjectVariationOrders/{variationOrder.Id}");
            try
            {
                var response = await client.PutAsJsonAsync(fullUrl, variationOrder);
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    throw new ConcurrencyException("Another user has modified this record.");
                }
                response.EnsureSuccessStatusCode();
            }
            catch (ConcurrencyException) { throw; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating variation order {Id} at {Url}", variationOrder.Id, fullUrl);
                throw;
            }
        }

        public async Task DeleteVariationOrderAsync(Guid id)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var fullUrl = GetFullUrl($"api/ProjectVariationOrders/{id}");
            try
            {
                var response = await client.DeleteAsync(fullUrl);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting variation order {Id} at {Url}", id, fullUrl);
                throw;
            }
        }
    }
}
