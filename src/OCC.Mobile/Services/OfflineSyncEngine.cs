using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using OCC.Shared.Framework;

namespace OCC.Mobile.Services
{
    public class OfflineSyncEngine : IOfflineSyncEngine
    {
        private readonly HttpClient _httpClient;
        private readonly string _storageFilePath;
        private readonly object _lock = new object();
        private List<SyncEntityChange> _pendingQueue = new List<SyncEntityChange>();

        public OfflineSyncEngine(HttpClient httpClient, string? customStoragePath = null)
        {
            _httpClient = httpClient ?? new HttpClient();

            var directory = customStoragePath ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OCC_Mobile");

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _storageFilePath = Path.Combine(directory, "offline_sync_queue.json");
            LoadQueueFromDisk();
        }

        public Task QueueChangeAsync(SyncEntityChange change)
        {
            if (change == null) throw new ArgumentNullException(nameof(change));

            lock (_lock)
            {
                _pendingQueue.Add(change);
                SaveQueueToDisk();
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SyncEntityChange>> GetPendingChangesAsync()
        {
            lock (_lock)
            {
                IReadOnlyList<SyncEntityChange> copy = _pendingQueue.ToList().AsReadOnly();
                return Task.FromResult(copy);
            }
        }

        public async Task<SyncPushResponse?> SyncPendingChangesAsync(string deviceId, string userId, string apiBaseUrl)
        {
            List<SyncEntityChange> itemsToSync;
            lock (_lock)
            {
                if (_pendingQueue.Count == 0)
                {
                    return new SyncPushResponse { Success = true, Message = "No pending changes to sync." };
                }

                itemsToSync = _pendingQueue.ToList();
            }

            var pushRequest = new SyncPushRequest
            {
                DeviceId = deviceId,
                UserId = userId,
                Changes = itemsToSync
            };

            try
            {
                var endpoint = apiBaseUrl.TrimEnd('/') + "/api/sync/push";
                var httpResponse = await _httpClient.PostAsJsonAsync(endpoint, pushRequest);

                if (httpResponse.IsSuccessStatusCode)
                {
                    var resultWrapper = await httpResponse.Content.ReadFromJsonAsync<ApiResponse<SyncPushResponse>>();
                    if (resultWrapper != null && resultWrapper.Success && resultWrapper.Data != null)
                    {
                        lock (_lock)
                        {
                            _pendingQueue.Clear();
                            SaveQueueToDisk();
                        }
                        return resultWrapper.Data;
                    }
                }
            }
            catch (Exception ex)
            {
                // Network unavailable or server unreachable - changes remain safely queued offline
                System.Diagnostics.Debug.WriteLine($"[OFFLINE-SYNC] Sync attempt failed: {ex.Message}");
            }

            return null;
        }

        public Task ClearPendingQueueAsync()
        {
            lock (_lock)
            {
                _pendingQueue.Clear();
                SaveQueueToDisk();
            }

            return Task.CompletedTask;
        }

        public Task<int> GetPendingCountAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_pendingQueue.Count);
            }
        }

        private void LoadQueueFromDisk()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_storageFilePath))
                    {
                        var json = File.ReadAllText(_storageFilePath);
                        var items = JsonSerializer.Deserialize<List<SyncEntityChange>>(json);
                        _pendingQueue = items ?? new List<SyncEntityChange>();
                    }
                }
                catch
                {
                    _pendingQueue = new List<SyncEntityChange>();
                }
            }
        }

        private void SaveQueueToDisk()
        {
            lock (_lock)
            {
                try
                {
                    var json = JsonSerializer.Serialize(_pendingQueue, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_storageFilePath, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OFFLINE-SYNC] Failed to save queue to disk: {ex.Message}");
                }
            }
        }
    }
}
