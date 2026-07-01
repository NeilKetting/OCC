using System;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.WpfClient.Infrastructure.Messages
{
    /// <summary>
    /// Message sent to request the shell to open a specific project hub and select a specific task.
    /// </summary>
    public class OpenProjectTaskMessage : ValueChangedMessage<(Guid ProjectId, Guid TaskId)>
    {
        public OpenProjectTaskMessage(Guid projectId, Guid taskId) : base((projectId, taskId))
        {
        }
    }
}
