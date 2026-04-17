using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OCC.Shared.Models;
using OCC.Shared.DTOs;
using OCC.Client.Services.Interfaces;
using OCC.Client.Services.Managers.Interfaces;
using OCC.Client.Services.Repositories.Interfaces;

namespace OCC.Client.Services.Managers
{
    /// <summary>
    /// Implementation of IProjectManager that provides centralized business logic for project and task management.
    /// This class acts as a bridge between the repositories and the ViewModels, handling complex operations
    /// like hierarchy building and expansion logic.
    /// </summary>
    public class ProjectManager : IProjectManager
    {
        private readonly IProjectService _projectService;
        private readonly IRepository<ProjectTask> _taskRepository;
        private readonly IRepository<Employee> _employeeRepository;

        /// <summary>
        /// Initializes a new instance of the ProjectManager class.
        /// </summary>
        /// <param name="projectService">The service for project data.</param>
        /// <param name="taskRepository">The repository for task data.</param>
        public ProjectManager(
            IProjectService projectService,
            IRepository<ProjectTask> taskRepository,
            IRepository<Employee> employeeRepository)
        {
            _projectService = projectService;
            _taskRepository = taskRepository;
            _employeeRepository = employeeRepository;
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<ProjectSummaryDto>> GetProjectsAsync()
        {
            return await _projectService.GetProjectSummariesAsync();
        }

        /// <inheritdoc/>
        public async Task<Project?> GetProjectByIdAsync(Guid id)
        {
            return await _projectService.GetProjectAsync(id);
        }

        /// <inheritdoc/>
        public async Task<IEnumerable<ProjectTask>> GetTasksForProjectAsync(Guid projectId)
        {
            return await _taskRepository.FindAsync(t => t.ProjectId == projectId);
        }

        /// <summary>
        /// Reconstructs the task hierarchy (parent-child relationships) from a flat list, 
        /// typically retrieved from the database. It uses the IndentLevel and relative order
        /// to infer the hierarchy, similar to how Microsoft Project (MSP) data is structured.
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
        /// Converts the hierarchical tree of tasks back into a flat list for UI display (e.g., DataGrid).
        /// Iterates only through the branches that are currently expanded by the user.
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

        /// <summary>
        /// Recursively adds a task and its visible children to the flat list.
        /// </summary>
        private void FlattenTask(ProjectTask task, List<ProjectTask> flatList, int level)
        {
            task.IndentLevel = level;
            flatList.Add(task);

            // Only traverse children if the parent task is expanded in the UI.
            if (task.IsExpanded && task.Children != null && task.Children.Any())
            {
                foreach (var child in task.Children)
                {
                    FlattenTask(child, flatList, level + 1);
                }
            }
        }

        /// <inheritdoc/>
        public async Task SaveTaskAsync(ProjectTask task)
        {
            if (task.Id == Guid.Empty)
            {
                await _taskRepository.AddAsync(task);
            }
            else
            {
                await _taskRepository.UpdateAsync(task);
            }
        }

        /// <inheritdoc/>
        public async Task DeleteProjectAsync(Guid projectId)
        {
            await _projectService.DeleteProjectAsync(projectId);
        }

        /// <inheritdoc/>
        public async Task DeleteTaskAsync(Guid taskId)
        {
            await _taskRepository.DeleteAsync(taskId);
        }

        /// <inheritdoc/>
        public void ToggleExpand(ProjectTask task)
        {
            if (task == null) return;
            task.IsExpanded = !task.IsExpanded;
        }

        /// <inheritdoc/>
        public async Task AssignSiteManagerAsync(Guid projectId, Guid managerId)
        {
            var project = await _projectService.GetProjectAsync(projectId);
            if (project != null)
            {
                project.SiteManagerId = managerId;
                await _projectService.UpdateProjectAsync(project);
            }
        }

        /// <inheritdoc/>
        public async Task<List<Employee>> GetSiteManagersAsync()
        {
            var employees = await _employeeRepository.GetAllAsync();
            return employees.Where(e => e.Role == EmployeeRole.SiteManager).ToList();
        }
    }
}
