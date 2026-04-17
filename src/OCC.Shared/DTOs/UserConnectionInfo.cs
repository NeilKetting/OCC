using System;

namespace OCC.Shared.DTOs
{
    public class UserConnectionInfo
    {
        public string UserName { get; set; } = string.Empty;
        public DateTime ConnectedAt { get; set; }
        public string Status { get; set; } = "Online";
    }
}
