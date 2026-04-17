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
    public class TaskServiceBase
    {
        protected readonly IHttpClientFactory HttpClientFactory;
        protected readonly IAuthService AuthService;
        protected readonly ConnectionSettings ConnectionSettings;

        public TaskServiceBase(IHttpClientFactory httpClientFactory, IAuthService authService, ConnectionSettings connectionSettings)
        {
            HttpClientFactory = httpClientFactory;
            AuthService = authService;
            ConnectionSettings = connectionSettings;
        }

        protected void EnsureAuthorization(HttpClient client)
        {
            var token = AuthService.CurrentToken;
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        protected string GetFullUrl(string path)
        {
            var baseUrl = ConnectionSettings.ApiBaseUrl ?? "http://localhost:5237/";
            if (!baseUrl.EndsWith("/")) baseUrl += "/";
            return $"{baseUrl}{path}";
        }
    }

    public class ProjectTaskService : TaskServiceBase, IProjectTaskService
    {
        private readonly ILogger<ProjectTaskService> _logger;
        public ProjectTaskService(IHttpClientFactory httpClientFactory, IAuthService authService, ConnectionSettings connectionSettings, ILogger<ProjectTaskService> logger) 
            : base(httpClientFactory, authService, connectionSettings) { _logger = logger; }

        public async Task<IEnumerable<ProjectTask>> GetTasksAsync(Guid? projectId = null, bool assignedToMe = false)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/ProjectTasks?projectId={projectId}&assignedToMe={assignedToMe}");
            return await client.GetFromJsonAsync<IEnumerable<ProjectTask>>(url) ?? new List<ProjectTask>();
        }

        public async Task<ProjectTask?> GetTaskAsync(Guid id)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/ProjectTasks/{id}");
            return await client.GetFromJsonAsync<ProjectTask>(url);
        }

        public async Task<ProjectTask> CreateTaskAsync(ProjectTask task)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/ProjectTasks");
            var response = await client.PostAsJsonAsync(url, task);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProjectTask>() ?? task;
        }

        public async Task UpdateTaskAsync(ProjectTask task)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/ProjectTasks/{task.Id}");
            var response = await client.PutAsJsonAsync(url, task);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteTaskAsync(Guid id)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/ProjectTasks/{id}");
            var response = await client.DeleteAsync(url);
            response.EnsureSuccessStatusCode();
        }
    }

    public class TaskAssignmentService : TaskServiceBase, ITaskAssignmentService
    {
        public TaskAssignmentService(IHttpClientFactory httpClientFactory, IAuthService authService, ConnectionSettings connectionSettings) 
            : base(httpClientFactory, authService, connectionSettings) { }

        public async Task<IEnumerable<TaskAssignment>> GetAssignmentsAsync(Guid taskId)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/TaskAssignments?taskId={taskId}");
            return await client.GetFromJsonAsync<IEnumerable<TaskAssignment>>(url) ?? new List<TaskAssignment>();
        }

        public async Task AddAssignmentAsync(TaskAssignment assignment)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/TaskAssignments");
            var response = await client.PostAsJsonAsync(url, assignment);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteAssignmentAsync(Guid id)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/TaskAssignments/{id}");
            var response = await client.DeleteAsync(url);
            response.EnsureSuccessStatusCode();
        }
    }

    public class TaskCommentService : TaskServiceBase, ITaskCommentService
    {
        public TaskCommentService(IHttpClientFactory httpClientFactory, IAuthService authService, ConnectionSettings connectionSettings) 
            : base(httpClientFactory, authService, connectionSettings) { }

        public async Task<IEnumerable<TaskComment>> GetCommentsAsync(Guid taskId)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/TaskComments?taskId={taskId}");
            return await client.GetFromJsonAsync<IEnumerable<TaskComment>>(url) ?? new List<TaskComment>();
        }

        public async Task AddCommentAsync(TaskComment comment)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/TaskComments");
            var response = await client.PostAsJsonAsync(url, comment);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteCommentAsync(Guid id)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/TaskComments/{id}");
            var response = await client.DeleteAsync(url);
            response.EnsureSuccessStatusCode();
        }
    }

    public class TaskAttachmentService : TaskServiceBase, ITaskAttachmentService
    {
        public TaskAttachmentService(IHttpClientFactory httpClientFactory, IAuthService authService, ConnectionSettings connectionSettings) 
            : base(httpClientFactory, authService, connectionSettings) { }

        public async Task<IEnumerable<TaskAttachment>> GetAttachmentsForTaskAsync(Guid taskId)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/TaskAttachments?taskId={taskId}");
            return await client.GetFromJsonAsync<IEnumerable<TaskAttachment>>(url) ?? new List<TaskAttachment>();
        }

        public async Task AddAttachmentAsync(TaskAttachment attachment)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl("api/TaskAttachments");
            var response = await client.PostAsJsonAsync(url, attachment);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteAttachmentAsync(Guid id)
        {
            var client = HttpClientFactory.CreateClient();
            EnsureAuthorization(client);
            var url = GetFullUrl($"api/TaskAttachments/{id}");
            var response = await client.DeleteAsync(url);
            response.EnsureSuccessStatusCode();
        }
    }
}
