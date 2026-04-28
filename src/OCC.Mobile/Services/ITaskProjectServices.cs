using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;
using OCC.Shared.DTOs;

namespace OCC.Mobile.Services
{
    public interface IProjectTaskService
    {
        Task<IEnumerable<ProjectTask>> GetTasksAsync(Guid? projectId = null, bool assignedToMe = false, int skip = 0, int take = 100);
        Task<ProjectTask?> GetTaskAsync(Guid id);
        Task UpdateTaskAsync(ProjectTask task);
        Task<IEnumerable<DashboardUpdateDto>> GetRecentUpdatesAsync();
    }

    public interface IProjectService
    {
        Task<IEnumerable<Project>> GetProjectsAsync(bool assignedToMe = false);
        Task<Project?> GetProjectAsync(Guid id);
    }

    public interface IInventoryService
    {
        Task<IEnumerable<InventoryItem>> GetProjectInventoryAsync(Guid projectId);
    }

    public interface ITeamService
    {
        Task<IEnumerable<ProjectTeamMember>> GetProjectTeamAsync(Guid projectId);
    }
}
