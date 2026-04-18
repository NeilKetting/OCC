using OCC.Shared.Models;
using OCC.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IProjectService
    {
        Task<IEnumerable<Project>> GetProjectsAsync();
        Task<IEnumerable<ProjectSummaryDto>> GetProjectSummariesAsync();
        Task<Project?> GetProjectAsync(Guid id);
        Task CreateProjectAsync(Project project);
        Task UpdateProjectAsync(Project project);
        Task<IEnumerable<ProjectTask>> GetProjectTasksAsync(Guid projectId);
        List<ProjectTask> BuildTaskHierarchy(IEnumerable<ProjectTask> allTasks);
        List<ProjectTask> FlattenHierarchy(IEnumerable<ProjectTask> rootTasks);
        void ToggleExpand(ProjectTask task);
    }
}
