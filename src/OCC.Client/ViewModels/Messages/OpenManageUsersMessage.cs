using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.Client.ViewModels.Messages
{
    public class OpenManageUsersMessage : ValueChangedMessage<System.Guid?>
    {
        public OpenManageUsersMessage(System.Guid? userId = null) : base(userId)
        {
        }
    }
}
