using CommunityToolkit.Mvvm.Messaging.Messages;
using System.Windows;

namespace OCC.WpfClient.Infrastructure.Messages
{
    public class WindowSizeInfo
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public WindowState State { get; set; } = WindowState.Normal;
    }

    public class ResizeWindowMessage : ValueChangedMessage<WindowSizeInfo>
    {
        public ResizeWindowMessage(WindowSizeInfo value) : base(value)
        {
        }
    }

    public class PreferenceChangedMessage
    {
        public string PreferenceName { get; }
        public PreferenceChangedMessage(string name)
        {
            PreferenceName = name;
        }
    }
}
