using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;
using OCC.Shared.DTOs;

namespace OCC.Client.Services.Managers.Interfaces
{
    /// <summary>
    /// Defines the contract for managing projects and their associated tasks.
    /// This manager centralizes business logic related to project lifecycle, task hierarchy,
    /// and data persistence, moving it out of the ViewModels for better maintainability.
    /// </summary>
    public interface IProjectManager
    {
        /// <summary>
        /// Retrieves all projects from the repository as lightweight summaries.
        /// </summary>
        /// <returns>A collection of project summaries.</returns>
        Task<IEnumerable<ProjectSummaryDto>> GetProjectsAsync();

        /// <summary>
        /// Retrieves a specific project by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the project.</param>
        /// <returns>The project if found; otherwise, null.</returns>
        Task<Project?> GetProjectByIdAsync(Guid id);

        /// <summary>
        /// Retrieves all tasks associated with a specific project.
        /// </summary>
        /// <param name="projectId">The unique identifier of the project.</param>
        /// <returns>A collection of tasks belonging to the project.</returns>
        Task<IEnumerable<ProjectTask>> GetTasksForProjectAsync(Guid projectId);
        
        /// <summary>
        /// Builds a tree structure (hierarchy) from a flat list of tasks based on their IndentLevel and order.
        /// This is used to display tasks in a nested format (e.g., in a Gantt chart or tree grid).
        /// </summary>
        /// <param name="allTasks">A flat collection of all tasks for a project.</param>
        /// <returns>A list of root-level tasks with their children populated.</returns>
        List<ProjectTask> BuildTaskHierarchy(IEnumerable<ProjectTask> allTasks);
        
        /// <summary>
        /// Flattens a hierarchy of tasks into a single-level list suitable for display in a flat control (like a DataGrid),
        /// while respecting the expansion state of parent tasks.
        /// </summary>
        /// <param name="rootTasks">The root tasks of the hierarchy.</param>
        /// <returns>A flat list of tasks that should be currently visible in the UI.</returns>
        List<ProjectTask> FlattenHierarchy(IEnumerable<ProjectTask> rootTasks);
        
        /// <summary>
        /// Saves or updates a project task in the persistent store.
        /// </summary>
        /// <param name="task">The task object to save.</param>
        Task SaveTaskAsync(ProjectTask task);

        /// <summary>
        /// Deletes a project and all its associated data.
        /// </summary>
        /// <param name="projectId">The unique identifier of the project to delete.</param>
        Task DeleteProjectAsync(Guid projectId);

        /// <summary>
        /// Deletes a specific project task.
        /// </summary>
        /// <param name="taskId">The ID of the task to delete.</param>
        Task DeleteTaskAsync(Guid taskId);

        /// <summary>
        /// Toggles the expanded/collapsed state of a task in the UI hierarchy.
        /// </summary>
        /// <param name="task">The task to toggle.</param>
        void ToggleExpand(ProjectTask task);

        /// <summary>
        /// Assigns a site manager to a project.
        /// </summary>
        /// <param name="projectId">The ID of the project.</param>
        /// <param name="managerId">The ID of the employee to assign as site manager.</param>
        Task AssignSiteManagerAsync(Guid projectId, Guid managerId);

        /// <summary>
        /// Retrieves a list of employees with the SiteManager role.
        /// </summary>
        /// <returns>A list of eligible site managers.</returns>
        Task<List<Employee>> GetSiteManagersAsync();
    }
}
