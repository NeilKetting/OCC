using CommunityToolkit.Mvvm.Messaging.Messages;
using OCC.Shared.Models;

namespace OCC.Client.ViewModels.Messages
{
    public class ProjectCreatedMessage : ValueChangedMessage<Project>
    {
        public ProjectCreatedMessage(Project project) : base(project)
        {
        }
    }
}
