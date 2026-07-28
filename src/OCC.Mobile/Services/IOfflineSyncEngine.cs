using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Framework;

namespace OCC.Mobile.Services
{
    /// <summary>
    /// Service contract for managing local offline queues and mobile synchronizations in OCC.Mobile.
    /// </summary>
    public interface IOfflineSyncEngine
    {
        Task QueueChangeAsync(SyncEntityChange change);
        Task<IReadOnlyList<SyncEntityChange>> GetPendingChangesAsync();
        Task<SyncPushResponse?> SyncPendingChangesAsync(string deviceId, string userId, string apiBaseUrl);
        Task ClearPendingQueueAsync();
        Task<int> GetPendingCountAsync();
    }
}
