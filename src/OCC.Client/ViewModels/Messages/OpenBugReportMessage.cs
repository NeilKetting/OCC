using CommunityToolkit.Mvvm.Messaging.Messages;
using System;

namespace OCC.Client.ViewModels.Messages
{
    public class OpenBugReportMessage : ValueChangedMessage<Guid?>
    {
        public OpenBugReportMessage(Guid? bugId) : base(bugId)
        {
        }
    }
}
