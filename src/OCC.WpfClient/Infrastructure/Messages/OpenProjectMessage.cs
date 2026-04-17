using System;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.WpfClient.Infrastructure.Messages
{
    /// <summary>
    /// Message sent to request the shell to open a specific project hub by its ID.
    /// </summary>
    public class OpenProjectMessage : ValueChangedMessage<Guid>
    {
        public OpenProjectMessage(Guid projectId) : base(projectId)
        {
        }
    }
}
