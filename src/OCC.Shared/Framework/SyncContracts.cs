using System;
using System.Collections.Generic;

namespace OCC.Shared.Framework
{
    public enum SyncAction
    {
        Create = 1,
        Update = 2,
        Delete = 3
    }

    public enum SyncConflictPolicy
    {
        ServerWins = 1,
        ClientWins = 2,
        ManualResolutionRequired = 3
    }

    /// <summary>
    /// Represents an individual entity change recorded offline on the mobile tablet or on the server.
    /// </summary>
    public class SyncEntityChange
    {
        public Guid ChangeId { get; set; } = Guid.NewGuid();
        public string EntityName { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public SyncAction Action { get; set; }
        public string JsonPayload { get; set; } = string.Empty;
        public DateTime ClientTimestampUtc { get; set; } = DateTime.UtcNow;
        public byte[]? RowVersion { get; set; }
    }

    /// <summary>
    /// Request sent from OCC.Mobile to OCC.API to push changes queued while working offline.
    /// </summary>
    public class SyncPushRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public List<SyncEntityChange> Changes { get; set; } = new List<SyncEntityChange>();
    }

    /// <summary>
    /// Individual status result for a pushed change.
    /// </summary>
    public class SyncChangeResult
    {
        public Guid ChangeId { get; set; }
        public Guid EntityId { get; set; }
        public bool Applied { get; set; }
        public bool ConflictDetected { get; set; }
        public string? ConflictMessage { get; set; }
        public string? ServerJsonPayload { get; set; }
        public DateTime ProcessedTimestampUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Response returned from OCC.API after processing offline push request.
    /// </summary>
    public class SyncPushResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<SyncChangeResult> Results { get; set; } = new List<SyncChangeResult>();
        public DateTime ServerSyncTimestampUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Request sent from OCC.Mobile to fetch delta changes from OCC.API since last sync.
    /// </summary>
    public class SyncPullRequest
    {
        public string DeviceId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime LastSyncTimestampUtc { get; set; }
        public List<string> EntityFilterNames { get; set; } = new List<string>();
    }

    /// <summary>
    /// Response returned from OCC.API containing delta changes for offline mobile sync.
    /// </summary>
    public class SyncPullResponse
    {
        public DateTime ServerSyncTimestampUtc { get; set; } = DateTime.UtcNow;
        public List<SyncEntityChange> DeltaChanges { get; set; } = new List<SyncEntityChange>();
        public bool HasMoreChanges { get; set; }
    }
}
