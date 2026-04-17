using CommunityToolkit.Mvvm.Messaging.Messages;

using OCC.Client.ViewModels.Core;

namespace OCC.Client.ViewModels.Messages
{
    public class NavigationMessage : ValueChangedMessage<ViewModelBase>
    {
        public NavigationMessage(ViewModelBase value) : base(value)
        {
        }
    }
}
