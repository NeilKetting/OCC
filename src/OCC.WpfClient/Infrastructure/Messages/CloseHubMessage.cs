using CommunityToolkit.Mvvm.Messaging.Messages;
using OCC.WpfClient.Infrastructure;

namespace OCC.WpfClient.Infrastructure.Messages
{
    public class CloseHubMessage : ValueChangedMessage<ViewModelBase>
    {
        public CloseHubMessage(ViewModelBase hub) : base(hub)
        {
        }
    }
}
