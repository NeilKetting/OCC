using CommunityToolkit.Mvvm.Messaging.Messages;
using OCC.Client.Models;

namespace OCC.Client.ViewModels.Messages
{
    public class ToastNotificationMessage : ValueChangedMessage<ToastMessage>
    {
        public ToastNotificationMessage(ToastMessage value) : base(value)
        {
        }
    }
}
