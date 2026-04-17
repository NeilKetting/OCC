using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.WpfClient.Infrastructure.Messages
{
    /// <summary>
    /// Message sent to request the shell to open a specific hub by its route string.
    /// </summary>
    public class OpenHubMessage : ValueChangedMessage<string>
    {
        public OpenHubMessage(string route) : base(route)
        {
        }
    }
}
