using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.WpfClient.Infrastructure.Messages
{
    public class StatusUpdateMessage : ValueChangedMessage<string>
    {
        public StatusUpdateMessage(string value) : base(value)
        {
        }
    }
}
