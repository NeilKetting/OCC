using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace OCC.Client.ViewModels.Messages
{
    public class ProjectDeletedMessage : ValueChangedMessage<Guid>
    {
        public ProjectDeletedMessage(Guid projectId) : base(projectId)
        {
        }
    }
}
