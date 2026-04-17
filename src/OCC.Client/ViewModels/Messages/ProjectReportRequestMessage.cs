using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace OCC.Client.ViewModels.Messages
{
    public class ProjectReportRequestMessage : ValueChangedMessage<Guid>
    {
        public ProjectReportRequestMessage(Guid projectId) : base(projectId)
        {
        }
    }
}
