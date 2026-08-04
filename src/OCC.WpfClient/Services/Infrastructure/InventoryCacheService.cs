using OCC.Shared.Models;
using OCC.WpfClient.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OCC.WpfClient.Services.Infrastructure
{
    /// <summary>
    /// Thread-safe in-memory TTL cache for the inventory item catalog.
    /// Shared between <c>PurchaseOrderDetailViewModel</c> and <c>PickingOrderViewModel</c>
    /// so that navigating between screens does not re-fetch the (potentially large)
    /// inventory list more than once per TTL window.
    /// </summary>
    /// <remarks>
    /// <b>Why TTL and not always-refresh?</b>
    /// Always refreshing on every load hits the API on every navigation event and is
    /// wasteful when the catalog has hundreds of items. A short TTL (default 5 min)
    /// means newly added SKUs are visible within the window, while eliminating repeated
    /// fetches during a normal order-entry session.
    /// </remarks>
    public class InventoryCacheService
    {
        private readonly IInventoryService _inventoryService;

        /// <summary>How long a cached list is considered fresh.</summary>
        private readonly TimeSpan _ttl;

        private List<InventoryItem>? _cache;
        private DateTime _lastFetched = DateTime.MinValue;

        /// <summary>Guards concurrent access during a cache miss refresh.</summary>
        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        /// <param name="inventoryService">The upstream inventory service.</param>
        /// <param name="ttl">
        /// Cache time-to-live. Defaults to 5 minutes if null.
        /// Pass <see cref="TimeSpan.Zero"/> to disable caching (always-refresh behaviour).
        /// </param>
        public InventoryCacheService(IInventoryService inventoryService, TimeSpan? ttl = null)
        {
            _inventoryService = inventoryService;
            _ttl = ttl ?? TimeSpan.FromMinutes(5);
        }

        /// <summary>
        /// Returns the cached inventory list if fresh, otherwise fetches from the API.
        /// Thread-safe — concurrent callers during a cache miss will wait for the single
        /// in-flight refresh to complete rather than issuing duplicate API requests.
        /// </summary>
        public async Task<IReadOnlyList<InventoryItem>> GetAsync()
        {
            // Fast path: cache is warm and within TTL
            if (_cache != null && (DateTime.UtcNow - _lastFetched) < _ttl)
            {
                return _cache;
            }

            await _refreshLock.WaitAsync();
            try
            {
                // Double-checked locking — another caller may have refreshed while we waited
                if (_cache != null && (DateTime.UtcNow - _lastFetched) < _ttl)
                {
                    return _cache;
                }

                _cache = new List<InventoryItem>(await _inventoryService.GetInventoryAsync());
                _lastFetched = DateTime.UtcNow;
                return _cache;
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        /// <summary>
        /// Adds a newly created item to the cache without requiring a full refresh.
        /// Call this after a new SKU is created via the "Create New Item" dialog.
        /// </summary>
        public void AddItem(InventoryItem item)
        {
            _cache?.Add(item);
        }

        /// <summary>
        /// Invalidates the cache, forcing the next call to <see cref="GetAsync"/> to
        /// fetch a fresh list from the API. Use when you know inventory has changed
        /// (e.g., after a stock receive event).
        /// </summary>
        public void Invalidate()
        {
            _lastFetched = DateTime.MinValue;
        }
    }
}
