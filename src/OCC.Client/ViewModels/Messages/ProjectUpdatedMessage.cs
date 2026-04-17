using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace OCC.Client.ViewModels.Messages
{
    public class ProjectUpdatedMessage : ValueChangedMessage<Guid>
    {
        public ProjectUpdatedMessage(Guid projectId) : base(projectId)
        {
        }
    }
}
