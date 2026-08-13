using System;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace OCC.WpfClient.Infrastructure.Messages
{
    public class OpenEmployeeParams
    {
        public Guid EmployeeId { get; set; }
        public string? FocusSection { get; set; }
    }

    public class OpenEmployeeMessage : ValueChangedMessage<OpenEmployeeParams>
    {
        public OpenEmployeeMessage(Guid employeeId, string? focusSection = null)
            : base(new OpenEmployeeParams { EmployeeId = employeeId, FocusSection = focusSection })
        {
        }
    }
}
