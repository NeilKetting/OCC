using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.Client.Messages
{
    public class NavigationRequestMessage : ValueChangedMessage<string>
    {
        public object? Payload { get; }
        
        public NavigationRequestMessage(string route, object? payload = null) : base(route)
        {
            Payload = payload;
        }
    }
}
