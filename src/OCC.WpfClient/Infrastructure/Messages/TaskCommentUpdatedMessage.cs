using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace OCC.WpfClient.Infrastructure.Messages
{
    public class TaskCommentUpdatedMessage : ValueChangedMessage<Guid>
    {
        public TaskCommentUpdatedMessage(Guid taskId) : base(taskId)
        {
        }
    }
}
