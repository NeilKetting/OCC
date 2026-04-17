using OCC.Shared.DTOs;
using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.Client.Services.Interfaces
{
    public interface IProjectService
    {
        Task<IEnumerable<ProjectSummaryDto>> GetProjectSummariesAsync();
        Task<Project?> GetProjectAsync(Guid id);
        Task<Project> CreateProjectAsync(Project project);
        Task<bool> UpdateProjectAsync(Project project);
        Task<bool> DeleteProjectAsync(Guid id);
        Task<ProjectReportDto?> GetProjectReportAsync(Guid id);
    }
}
