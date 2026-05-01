using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.WpfClient.Infrastructure.Messages
{
    public class SwitchTabMessage : ValueChangedMessage<string>
    {
        public SwitchTabMessage(string value) : base(value)
        {
        }
    }
}
