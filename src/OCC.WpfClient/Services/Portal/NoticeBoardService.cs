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
    public class NoticeBoardService : INoticeBoardService
    {
        private readonly ILogger<NoticeBoardService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ConnectionSettings _connectionSettings;
        private readonly IAuthService _authService;

        public NoticeBoardService(ILogger<NoticeBoardService> logger,
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

        public async Task<IEnumerable<NoticeBoardItem>> GetActiveNoticesAsync()
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/NoticeBoard");
            try
            {
                return await _httpClient.GetFromJsonAsync<IEnumerable<NoticeBoardItem>>(url) ?? new List<NoticeBoardItem>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching notices from {Url}", url);
                return new List<NoticeBoardItem>();
            }
        }

        public async Task<NoticeBoardItem> CreateNoticeAsync(NoticeBoardItem item)
        {
            EnsureAuthorization();
            var url = GetFullUrl("api/NoticeBoard");
            try
            {
                var response = await _httpClient.PostAsJsonAsync(url, item);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<NoticeBoardItem>() ?? item;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating notice at {Url}", url);
                throw;
            }
        }

        public async Task<bool> DeleteNoticeAsync(Guid id)
        {
            EnsureAuthorization();
            var url = GetFullUrl($"api/NoticeBoard/{id}");
            try
            {
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting notice {Id} at {Url}", id, url);
                throw;
            }
        }
    }
}
