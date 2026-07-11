using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OCC.Shared.DTOs;
using OCC.WpfClient.Services.Interfaces;

namespace OCC.WpfClient.Services.Infrastructure
{
    public class TodoService : ITodoService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;
        private readonly IOutlookService _outlookService;
        private readonly ILogger<TodoService> _logger;
        private readonly ConnectionSettings _connectionSettings;

        public TodoService(
            IHttpClientFactory httpClientFactory,
            IAuthService authService,
            IOutlookService outlookService,
            ILogger<TodoService> logger,
            ConnectionSettings connectionSettings)
        {
            _httpClient = httpClientFactory.CreateClient();
            _authService = authService;
            _outlookService = outlookService;
            _logger = logger;
            _connectionSettings = connectionSettings;
        }

        private string GetFullUrl(string path)
        {
            var baseUrl = _connectionSettings.ApiBaseUrl;
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

        public async Task<List<PersonalTodoDto>> GetTodosAsync()
        {
            try
            {
                EnsureAuthorization();
                var response = await _httpClient.GetFromJsonAsync<List<PersonalTodoDto>>(GetFullUrl("api/PersonalTodos"));
                return response ?? new List<PersonalTodoDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load to-dos from server.");
                return new List<PersonalTodoDto>();
            }
        }

        public async Task<PersonalTodoDto> GetTodoAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.GetFromJsonAsync<PersonalTodoDto>(GetFullUrl($"api/PersonalTodos/{id}"));
            if (response == null) throw new InvalidOperationException("To-do not found.");
            return response;
        }

        public async Task<PersonalTodoDto> CreateTodoAsync(CreatePersonalTodoDto dto)
        {
            string? outlookId = null;
            if (dto.DueDate.HasValue)
            {
                outlookId = _outlookService.SyncTodoToCalendar(dto.Title, dto.Notes, dto.DueDate.Value, null);
                dto.OutlookEventId = outlookId;
            }

            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync(GetFullUrl("api/PersonalTodos"), dto);
            response.EnsureSuccessStatusCode();

            var created = await response.Content.ReadFromJsonAsync<PersonalTodoDto>();
            if (created == null) throw new InvalidOperationException("Failed to parse created to-do.");
            return created;
        }

        public async Task UpdateTodoAsync(Guid id, UpdatePersonalTodoDto dto)
        {
            if (dto.IsComplete || !dto.DueDate.HasValue)
            {
                if (!string.IsNullOrEmpty(dto.OutlookEventId))
                {
                    _outlookService.DeleteEvent(dto.OutlookEventId);
                    dto.OutlookEventId = null;
                }
            }
            else if (dto.DueDate.HasValue)
            {
                string? outlookId = _outlookService.SyncTodoToCalendar(dto.Title, dto.Notes, dto.DueDate.Value, dto.OutlookEventId);
                dto.OutlookEventId = outlookId;
            }

            EnsureAuthorization();
            var response = await _httpClient.PutAsJsonAsync(GetFullUrl($"api/PersonalTodos/{id}"), dto);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteTodoAsync(Guid id)
        {
            try
            {
                var todo = await GetTodoAsync(id);
                if (!string.IsNullOrEmpty(todo.OutlookEventId))
                {
                    _outlookService.DeleteEvent(todo.OutlookEventId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch to-do to delete its Outlook event.");
            }

            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync(GetFullUrl($"api/PersonalTodos/{id}"));
            response.EnsureSuccessStatusCode();
        }
    }
}
