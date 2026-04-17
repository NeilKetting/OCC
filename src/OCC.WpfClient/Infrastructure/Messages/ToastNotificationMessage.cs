using CommunityToolkit.Mvvm.Messaging.Messages;
using OCC.WpfClient.Models;

namespace OCC.WpfClient.Infrastructure.Messages
{
    public class ToastNotificationMessage : ValueChangedMessage<ToastMessage>
    {
        public ToastNotificationMessage(ToastMessage value) : base(value)
        {
        }
    }
}
