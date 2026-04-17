using System;

namespace OCC.Client.ViewModels.Messages
{
    public class TaskSelectedMessage
    {
        public Guid TaskId { get; }

        public TaskSelectedMessage(Guid taskId)
        {
            TaskId = taskId;
        }
    }
}
