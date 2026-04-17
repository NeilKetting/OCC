using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.Client.Services.Interfaces
{
    public interface ITaskAttachmentService
    {
        Task<IEnumerable<TaskAttachment>> GetAttachmentsForTaskAsync(Guid taskId);
        
        Task<TaskAttachment?> UploadAttachmentAsync(TaskAttachment metadata, Stream fileStream, string fileName);
        
        Task<bool> DeleteAttachmentAsync(Guid id);
    }
}
