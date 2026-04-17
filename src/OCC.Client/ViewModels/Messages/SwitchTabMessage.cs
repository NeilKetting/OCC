using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.Client.ViewModels.Messages
{
    public class SwitchTabMessage : ValueChangedMessage<string>
    {
        public SwitchTabMessage(string value) : base(value)
        {
        }
    }
}
