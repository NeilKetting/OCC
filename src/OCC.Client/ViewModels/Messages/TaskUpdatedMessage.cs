using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace OCC.Client.ViewModels.Messages
{
    public class TaskUpdatedMessage : ValueChangedMessage<Guid>
    {
        public TaskUpdatedMessage(Guid taskId) : base(taskId)
        {
        }
    }
}
