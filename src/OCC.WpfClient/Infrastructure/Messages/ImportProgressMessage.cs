using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.WpfClient.Infrastructure.Messages
{
    public class ImportProgressMessage : ValueChangedMessage<ImportProgressInfo>
    {
        public ImportProgressMessage(ImportProgressInfo value) : base(value)
        {
        }
    }

    public class ImportProgressInfo
    {
        public string Message { get; set; } = string.Empty;
        public double Progress { get; set; } // 0 to 100
        public bool IsComplete { get; set; }
        public bool IsVisible { get; set; }
    }
}
