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
    public class ProjectService : IProjectService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthService _authService;
        private readonly ILogger<ProjectService> _logger;
        private readonly ConnectionSettings _connectionSettings;

        public ProjectService(IHttpClientFactory httpClientFactory, IAuthService authService, ILogger<ProjectService> logger, ConnectionSettings connectionSettings)
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

        public async Task<IEnumerable<Project>> GetProjectsAsync()
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/Projects");
            try
            {
                return await client.GetFromJsonAsync<IEnumerable<Project>>(url) ?? new List<Project>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching projects from {Url}", url);
                throw;
            }
        }

        public async Task<IEnumerable<ProjectSummaryDto>> GetProjectSummariesAsync()
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/Projects/summaries");
            try
            {
                return await client.GetFromJsonAsync<IEnumerable<ProjectSummaryDto>>(url) ?? new List<ProjectSummaryDto>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching project summaries from {Url}", url);
                throw;
            }
        }

        public async Task<Project?> GetProjectAsync(Guid id)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/Projects/{id}");
            try
            {
                return await client.GetFromJsonAsync<Project>(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching project {Id} from {Url}", id, url);
                throw;
            }
        }

        public async Task CreateProjectAsync(Project project)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/Projects");
            try
            {
                var response = await client.PostAsJsonAsync(url, project);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating project at {Url}", url);
                throw;
            }
        }

        public async Task UpdateProjectAsync(Project project)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/Projects/{project.Id}");
            try
            {
                var response = await client.PutAsJsonAsync(url, project);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating project {Id} at {Url}", project.Id, url);
                throw;
            }
        }

        public async Task<IEnumerable<ProjectTask>> GetProjectTasksAsync(Guid projectId)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/ProjectTasks?projectId={projectId}");
            try
            {
                return await client.GetFromJsonAsync<IEnumerable<ProjectTask>>(url) ?? new List<ProjectTask>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching tasks for project {ProjectId} from {Url}", projectId, url);
                throw;
            }
        }
    }
}
