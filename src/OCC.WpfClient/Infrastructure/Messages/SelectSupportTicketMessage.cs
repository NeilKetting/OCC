using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace OCC.WpfClient.Infrastructure.Messages
{
    /// <summary>
    /// Message sent to trigger selecting a specific support ticket (bug report) in the SupportHub.
    /// </summary>
    public class SelectSupportTicketMessage : ValueChangedMessage<Guid>
    {
        public SelectSupportTicketMessage(Guid bugId) : base(bugId)
        {
        }
    }
}
