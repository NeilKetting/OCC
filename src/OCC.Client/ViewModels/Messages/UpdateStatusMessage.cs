using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.Client.ViewModels.Messages
{
    public class UpdateStatusMessage : ValueChangedMessage<string>
    {
        public UpdateStatusMessage(string value) : base(value)
        {
        }
    }
}
