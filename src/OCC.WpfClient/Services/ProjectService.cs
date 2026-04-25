using Microsoft.Extensions.Logging;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.WpfClient.Services.Infrastructure;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public async Task DeleteProjectAsync(Guid id)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/Projects/{id}");
            try
            {
                var response = await client.DeleteAsync(url);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting project {Id} at {Url}", id, url);
                throw;
            }
        }

        public async Task<ProjectPersonnelDto?> GetProjectPersonnelAsync(Guid projectId)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/projects/{projectId}/personnel");
            try
            {
                return await client.GetFromJsonAsync<ProjectPersonnelDto>(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching personnel for project {ProjectId} from {Url}", projectId, url);
                throw;
            }
        }

        public async Task UpdateProjectPersonnelAsync(Guid projectId, ProjectPersonnelUpdateDto update)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/projects/{projectId}/personnel");
            try
            {
                var response = await client.PostAsJsonAsync(url, update);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating personnel for project {ProjectId} at {Url}", projectId, url);
                throw;
            }
        }

        public async Task<ProjectHistoryDto> GetProjectHistoryAsync(Guid projectId)
        {
            var client = _httpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/projects/{projectId}/history");
            try
            {
                return await client.GetFromJsonAsync<ProjectHistoryDto>(url) ?? new ProjectHistoryDto { ProjectId = projectId };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching history for project {ProjectId} from {Url}", projectId, url);
                return new ProjectHistoryDto { ProjectId = projectId };
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

        /// <summary>
        /// Reconstructs the task hierarchy (parent-child relationships) from a flat list.
        /// </summary>
        public List<ProjectTask> BuildTaskHierarchy(IEnumerable<ProjectTask> allTasks)
        {
            var taskList = allTasks.ToList();

            foreach (var task in taskList)
            {
                task.Children.Clear();
            }

            var rootTasks = new List<ProjectTask>();
            var lookup = taskList.ToDictionary(t => t.Id);

            foreach (var task in taskList)
            {
                if (task.ParentId.HasValue && task.ParentId != Guid.Empty && lookup.TryGetValue(task.ParentId.Value, out var parent))
                {
                    parent.Children.Add(task);
                }
                else
                {
                    rootTasks.Add(task);
                }
            }

            foreach (var task in taskList)
            {
                if (task.Children.Any())
                {
                    task.Children = task.Children.OrderBy(c => c.OrderIndex).ToList();
                }
            }

            return rootTasks.OrderBy(t => t.OrderIndex).ToList();
        }

        /// <summary>
        /// Converts the hierarchical tree of tasks back into a flat list for UI display.
        /// </summary>
        public List<ProjectTask> FlattenHierarchy(IEnumerable<ProjectTask> rootTasks)
        {
            var flatList = new List<ProjectTask>();
            foreach (var rootTask in rootTasks)
            {
                FlattenTask(rootTask, flatList, 0);
            }
            return flatList;
        }

        private void FlattenTask(ProjectTask task, List<ProjectTask> flatList, int level)
        {
            task.IndentLevel = level;
            flatList.Add(task);

            if (task.IsExpanded && task.Children != null && task.Children.Any())
            {
                foreach (var child in task.Children)
                {
                    FlattenTask(child, flatList, level + 1);
                }
            }
        }

        public void ToggleExpand(ProjectTask task)
        {
            if (task == null) return;
            task.IsExpanded = !task.IsExpanded;
        }
    }
}
