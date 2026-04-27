using Microsoft.AspNetCore.SignalR;
using OCC.API.Hubs;

namespace OCC.API.Services
{
    public class SignalRHeartbeatService : BackgroundService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly ILogger<SignalRHeartbeatService> _logger;

        public SignalRHeartbeatService(IHubContext<NotificationHub> hubContext, ILogger<SignalRHeartbeatService> logger)
        {
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SignalR Heartbeat Service started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Send a heartbeat to all clients to keep connections alive and verify connectivity
                    await _hubContext.Clients.All.SendAsync("Heartbeat", DateTime.UtcNow, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to send SignalR heartbeat");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
