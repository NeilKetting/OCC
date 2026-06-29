using System;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.WpfClient.Infrastructure.Messages
{
    /// <summary>
    /// Message sent to request the shell to open a specific chat session by its ID.
    /// </summary>
    public class OpenChatSessionMessage : ValueChangedMessage<Guid>
    {
        public OpenChatSessionMessage(Guid sessionId) : base(sessionId)
        {
        }
    }
}
