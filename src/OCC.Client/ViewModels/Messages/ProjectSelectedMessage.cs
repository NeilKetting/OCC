using CommunityToolkit.Mvvm.Messaging.Messages;
using OCC.Shared.Models;

namespace OCC.Client.ViewModels.Messages
{
    public class ProjectSelectedMessage : ValueChangedMessage<Project>
    {
        public ProjectSelectedMessage(Project project) : base(project)
        {
        }
    }
}
