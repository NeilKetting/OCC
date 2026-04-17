using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace OCC.Client.ViewModels.Messages
{
    public class CreateNewTaskMessage
    {
        public Guid? ProjectId { get; }
        public DateTime? InitialDate { get; }

        public CreateNewTaskMessage(Guid? projectId = null, DateTime? initialDate = null)
        {
            ProjectId = projectId;
            InitialDate = initialDate;
        }
    }
}
