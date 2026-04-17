using OCC.Client.Services.Interfaces;
using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace OCC.Client.Services
{
    public class ProjectService : IProjectService
    {
        private readonly HttpClient _httpClient;
        private readonly IAuthService _authService;

        public ProjectService(HttpClient httpClient, IAuthService authService)
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

        public async Task<IEnumerable<ProjectSummaryDto>> GetProjectSummariesAsync()
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<IEnumerable<ProjectSummaryDto>>("api/Projects/summaries") ?? new List<ProjectSummaryDto>();
        }

        public async Task<Project?> GetProjectAsync(Guid id)
        {
            EnsureAuthorization();
            return await _httpClient.GetFromJsonAsync<Project>($"api/Projects/{id}");
        }

        public async Task<Project> CreateProjectAsync(Project project)
        {
            EnsureAuthorization();
            var response = await _httpClient.PostAsJsonAsync("api/Projects", project);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Project>() ?? project;
        }

        public async Task<bool> UpdateProjectAsync(Project project)
        {
            EnsureAuthorization();
            var response = await _httpClient.PutAsJsonAsync($"api/Projects/{project.Id}", project);
            return response.IsSuccessStatusCode;
        }

        public async Task<bool> DeleteProjectAsync(Guid id)
        {
            EnsureAuthorization();
            var response = await _httpClient.DeleteAsync($"api/Projects/{id}");
            return response.IsSuccessStatusCode;
        }

        public async Task<ProjectReportDto?> GetProjectReportAsync(Guid id)
        {
            EnsureAuthorization();
            try
            {
                return await _httpClient.GetFromJsonAsync<ProjectReportDto>($"api/Projects/{id}/report");
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
