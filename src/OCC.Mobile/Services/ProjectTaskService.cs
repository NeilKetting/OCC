using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Diagnostics;
using OCC.Shared.Models;
using OCC.Shared.DTOs;

namespace OCC.Mobile.Services
{
    public class ProjectTaskService : IProjectTaskService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;
        private readonly ILocalSettingsService _settingsService;

        public ProjectTaskService(IAuthService authService, ILocalSettingsService settingsService)
        {
            _authService = authService;
            _settingsService = settingsService;
            _httpClient = new HttpClient();
        }

        private string GetBaseUrl()
        {
            if (_settingsService.Settings.SelectedEnvironment == AppEnvironment.Local)
            {
                if (!string.IsNullOrEmpty(_settingsService.Settings.CustomLocalUrl))
                {
                    var url = _settingsService.Settings.CustomLocalUrl.Trim();
                    if (!url.EndsWith("/")) url += "/";
                    return url;
                }

                #if ANDROID
                return "http://10.0.2.2:5237/";
                #else
                return "http://localhost:5237/";
                #endif
            }
            return "http://102.221.36.149:8081/";
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<IEnumerable<ProjectTask>> GetTasksAsync(Guid? projectId = null, bool assignedToMe = false, int skip = 0, int take = 100)
        {
            try
            {
                EnsureAuthorization();
                var baseUrl = GetBaseUrl();
                var url = $"{baseUrl}api/ProjectTasks?projectId={projectId}&assignedToMe={assignedToMe}&skip={skip}&take={take}";
                var tasks = await _httpClient.GetFromJsonAsync<IEnumerable<ProjectTask>>(url) ?? new List<ProjectTask>();
                System.Diagnostics.Debug.WriteLine($"FETCHED {tasks.Count()} TASKS FROM {url}");
                return tasks;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR FETCHING TASKS: {ex.Message}");
                return new List<ProjectTask>();
            }
        }

        public async Task<ProjectTask?> GetTaskAsync(Guid id)
        {
            try
            {
                EnsureAuthorization();
                var baseUrl = GetBaseUrl();
                var url = $"{baseUrl}api/ProjectTasks/{id}";
                return await _httpClient.GetFromJsonAsync<ProjectTask>(url);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task UpdateTaskAsync(ProjectTask task)
        {
            EnsureAuthorization();
            var baseUrl = GetBaseUrl();
            var url = $"{baseUrl}api/ProjectTasks/{task.Id}";
            System.Diagnostics.Debug.WriteLine($"[MOBILE-API] Sending PUT to {url} with Status: {task.Status}");
            var response = await _httpClient.PutAsJsonAsync(url, task);
            System.Diagnostics.Debug.WriteLine($"[MOBILE-API] Response: {response.StatusCode}");
            response.EnsureSuccessStatusCode();
        }

        public async Task<IEnumerable<DashboardUpdateDto>> GetRecentUpdatesAsync()
        {
            try
            {
                EnsureAuthorization();
                var baseUrl = GetBaseUrl();
                var url = $"{baseUrl}api/ProjectTasks/recent-updates";
                return await _httpClient.GetFromJsonAsync<IEnumerable<DashboardUpdateDto>>(url) ?? new List<DashboardUpdateDto>();
            }
            catch (Exception)
            {
                return new List<DashboardUpdateDto>();
            }
        }
    }

    public class ProjectService : IProjectService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;
        private readonly ILocalSettingsService _settingsService;

        public ProjectService(IAuthService authService, ILocalSettingsService settingsService)
        {
            _authService = authService;
            _settingsService = settingsService;
            _httpClient = new HttpClient();
        }

        private string GetBaseUrl()
        {
            if (_settingsService.Settings.SelectedEnvironment == AppEnvironment.Local)
            {
                if (!string.IsNullOrEmpty(_settingsService.Settings.CustomLocalUrl))
                {
                    var url = _settingsService.Settings.CustomLocalUrl.Trim();
                    if (!url.EndsWith("/")) url += "/";
                    return url;
                }

                #if ANDROID
                return "http://10.0.2.2:5237/";
                #else
                return "http://localhost:5237/";
                #endif
            }
            return "http://102.221.36.149:8081/";
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<IEnumerable<Project>> GetProjectsAsync(bool assignedToMe = false)
        {
            try
            {
                EnsureAuthorization();
                var baseUrl = GetBaseUrl();
                var url = $"{baseUrl}api/Projects?assignedToMe={assignedToMe}";
                return await _httpClient.GetFromJsonAsync<IEnumerable<Project>>(url) ?? new List<Project>();
            }
            catch (Exception)
            {
                return new List<Project>();
            }
        }

        public async Task<Project?> GetProjectAsync(Guid id)
        {
            try
            {
                EnsureAuthorization();
                var baseUrl = GetBaseUrl();
                var url = $"{baseUrl}api/Projects/{id}";
                return await _httpClient.GetFromJsonAsync<Project>(url);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public class InventoryService : IInventoryService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;
        private readonly ILocalSettingsService _settingsService;

        public InventoryService(IAuthService authService, ILocalSettingsService settingsService)
        {
            _authService = authService;
            _settingsService = settingsService;
            _httpClient = new HttpClient();
        }

        private string GetBaseUrl()
        {
            if (_settingsService.Settings.SelectedEnvironment == AppEnvironment.Local)
            {
                if (!string.IsNullOrEmpty(_settingsService.Settings.CustomLocalUrl))
                {
                    var url = _settingsService.Settings.CustomLocalUrl.Trim();
                    if (!url.EndsWith("/")) url += "/";
                    return url;
                }

                #if ANDROID
                return "http://10.0.2.2:5237/";
                #else
                return "http://localhost:5237/";
                #endif
            }
            return "http://102.221.36.149:8081/";
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<IEnumerable<InventoryItem>> GetProjectInventoryAsync(Guid projectId)
        {
            try
            {
                EnsureAuthorization();
                var baseUrl = GetBaseUrl();
                var url = $"{baseUrl}api/Inventory/project/{projectId}";
                return await _httpClient.GetFromJsonAsync<IEnumerable<InventoryItem>>(url) ?? new List<InventoryItem>();
            }
            catch (Exception)
            {
                return new List<InventoryItem>();
            }
        }
    }

    public class TeamService : ITeamService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;
        private readonly ILocalSettingsService _settingsService;

        public TeamService(IAuthService authService, ILocalSettingsService settingsService)
        {
            _authService = authService;
            _settingsService = settingsService;
            _httpClient = new HttpClient();
        }

        private string GetBaseUrl()
        {
            if (_settingsService.Settings.SelectedEnvironment == AppEnvironment.Local)
            {
                if (!string.IsNullOrEmpty(_settingsService.Settings.CustomLocalUrl))
                {
                    var url = _settingsService.Settings.CustomLocalUrl.Trim();
                    if (!url.EndsWith("/")) url += "/";
                    return url;
                }

                #if ANDROID
                return "http://10.0.2.2:5237/";
                #else
                return "http://localhost:5237/";
                #endif
            }
            return "http://102.221.36.149:8081/";
        }

        private void EnsureAuthorization()
        {
            var token = _authService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<IEnumerable<ProjectTeamMember>> GetProjectTeamAsync(Guid projectId)
        {
            try
            {
                EnsureAuthorization();
                var baseUrl = GetBaseUrl();
                var url = $"{baseUrl}api/TeamMembers/project/{projectId}";
                return await _httpClient.GetFromJsonAsync<IEnumerable<ProjectTeamMember>>(url) ?? new List<ProjectTeamMember>();
            }
            catch (Exception)
            {
                return new List<ProjectTeamMember>();
            }
        }
    }
}
