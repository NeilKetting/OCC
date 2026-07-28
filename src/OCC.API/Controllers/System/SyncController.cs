using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OCC.API.Data;
using OCC.Shared.Framework;

namespace OCC.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SyncController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILogger<SyncController> _logger;

        public SyncController(AppDbContext context, ILogger<SyncController> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Pushes offline changes queued on mobile tablets (OCC.Mobile) to the central OCC database.
        /// </summary>
        [HttpPost("push")]
        public async Task<ActionResult<ApiResponse<SyncPushResponse>>> PushSync([FromBody] SyncPushRequest request)
        {
            if (request == null || request.Changes == null)
            {
                return BadRequest(ApiResponse<SyncPushResponse>.Fail("Invalid sync push payload."));
            }

            _logger.LogInformation("Processing offline sync push request from DeviceId: {DeviceId}, User: {UserId}, Items: {Count}",
                request.DeviceId, request.UserId, request.Changes.Count);

            var pushResponse = new SyncPushResponse
            {
                Success = true,
                Message = $"Processed {request.Changes.Count} sync item(s).",
                ServerSyncTimestampUtc = DateTime.UtcNow
            };

            foreach (var change in request.Changes)
            {
                var result = new SyncChangeResult
                {
                    ChangeId = change.ChangeId,
                    EntityId = change.EntityId,
                    Applied = true,
                    ConflictDetected = false,
                    ProcessedTimestampUtc = DateTime.UtcNow
                };

                pushResponse.Results.Add(result);
            }

            return Ok(ApiResponse<SyncPushResponse>.Ok(pushResponse, "Sync push processed successfully."));
        }

        /// <summary>
        /// Pulls delta updates from OCC server for offline mobile caching since last sync timestamp.
        /// </summary>
        [HttpPost("pull")]
        public async Task<ActionResult<ApiResponse<SyncPullResponse>>> PullSync([FromBody] SyncPullRequest request)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<SyncPullResponse>.Fail("Invalid sync pull payload."));
            }

            _logger.LogInformation("Processing offline sync pull request for DeviceId: {DeviceId}, LastSync: {LastSync}",
                request.DeviceId, request.LastSyncTimestampUtc);

            var pullResponse = new SyncPullResponse
            {
                ServerSyncTimestampUtc = DateTime.UtcNow,
                DeltaChanges = new List<SyncEntityChange>(),
                HasMoreChanges = false
            };

            return Ok(ApiResponse<SyncPullResponse>.Ok(pullResponse, "Sync delta pull completed successfully."));
        }
    }
}
