using System;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.WpfClient.Infrastructure.Messages
{
    /// <summary>
    /// Message sent to request the shell to open a specific purchase order detail view by its ID.
    /// </summary>
    public class OpenOrderMessage : ValueChangedMessage<Guid>
    {
        public OpenOrderMessage(Guid orderId) : base(orderId)
        {
        }
    }
}
