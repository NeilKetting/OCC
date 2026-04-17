using OCC.Shared.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IProjectTaskService
    {
        Task<IEnumerable<ProjectTask>> GetTasksAsync(Guid? projectId = null, bool assignedToMe = false);
        Task<ProjectTask?> GetTaskAsync(Guid id);
        Task<ProjectTask> CreateTaskAsync(ProjectTask task);
        Task UpdateTaskAsync(ProjectTask task);
        Task DeleteTaskAsync(Guid id);
    }

    public interface ITaskAssignmentService
    {
        Task<IEnumerable<TaskAssignment>> GetAssignmentsAsync(Guid taskId);
        Task AddAssignmentAsync(TaskAssignment assignment);
        Task DeleteAssignmentAsync(Guid id);
    }

    public interface ITaskCommentService
    {
        Task<IEnumerable<TaskComment>> GetCommentsAsync(Guid taskId);
        Task AddCommentAsync(TaskComment comment);
        Task DeleteCommentAsync(Guid id);
    }

    public interface ITaskAttachmentService
    {
        Task<IEnumerable<TaskAttachment>> GetAttachmentsForTaskAsync(Guid taskId);
        Task AddAttachmentAsync(TaskAttachment attachment);
        Task DeleteAttachmentAsync(Guid id);
    }
}
