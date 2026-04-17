using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.Client.Messages
{
    public class LogUploadStatusMessage : ValueChangedMessage<string>
    {
        public bool IsUploading { get; }
        public bool IsSuccess { get; }
        public bool IsError { get; }

        public LogUploadStatusMessage(string status, bool isUploading, bool isSuccess = false, bool isError = false) : base(status)
        {
            IsUploading = isUploading;
            IsSuccess = isSuccess;
            IsError = isError;
        }
    }
}
