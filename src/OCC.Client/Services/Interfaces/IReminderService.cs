using System;
using System.Threading.Tasks;

namespace OCC.Client.Services.Interfaces
{
    public interface IReminderService
    {
        event EventHandler<int> UnreadCountChanged;
        int UnreadCount { get; }
        
        void Start();
        void Stop();
        Task CheckRemindersAsync();
        void MarkAsRead(Guid taskId);
    }
}
