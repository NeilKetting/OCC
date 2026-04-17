using System;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.Client.ViewModels.Messages
{
    public class ProjectSettingsRequestedMessage
    {
        public Guid ProjectId { get; }

        public ProjectSettingsRequestedMessage(Guid projectId)
        {
            ProjectId = projectId;
        }
    }
}
