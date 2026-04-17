using Microsoft.AspNetCore.SignalR;

namespace OCC.API.Hubs
{
    public class NotificationHub : Hub
    {
        // Track connected users: ConnectionId -> UserConnectionInfo
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, OCC.Shared.DTOs.UserConnectionInfo> _connectedUsers 
            = new System.Collections.Concurrent.ConcurrentDictionary<string, OCC.Shared.DTOs.UserConnectionInfo>();

        public override async Task OnConnectedAsync()
        {
            try
            {
                var userName = Context.User?.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value 
                               ?? Context.User?.Identity?.Name;
                
                if (string.IsNullOrEmpty(userName)) userName = "Anonymous";

                var id = Context.ConnectionId;
                
                var info = new OCC.Shared.DTOs.UserConnectionInfo 
                { 
                    UserName = userName, 
                    ConnectedAt = DateTime.UtcNow,
                    Status = "Online"
                };

                _connectedUsers.TryAdd(id, info);
                await BroadcastUserList();
            }
            catch (Exception)
            {
                // Log?
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            try
            {
                var id = Context.ConnectionId;
                _connectedUsers.TryRemove(id, out _);
                await BroadcastUserList();
            }
            catch { }
            
            await base.OnDisconnectedAsync(exception);
        }

        private async Task BroadcastUserList()
        {
            // Distinct users by name. Prioritize "Online" status if multiple connections exist.
            var users = _connectedUsers.Values
                .Where(u => u.UserName != "Anonymous")
                .GroupBy(u => u.UserName)
                .Select(g => 
                {
                     // If any connection is Online, show Online. Else show Away.
                     var active = g.FirstOrDefault(x => x.Status == "Online") ?? g.First();
                     return active;
                })
                .OrderBy(u => u.UserName)
                .ToList();
                
            await Clients.All.SendAsync("UserListUpdate", users);
        }

        public async Task SendNotification(string message)
        {
            await Clients.All.SendAsync("ReceiveNotification", message);
        }

        public async Task SendBroadcast(string sender, string message)
        {
            await Clients.All.SendAsync("ReceiveBroadcast", sender, message);
        }

        public async Task UpdateStatus(string status)
        {
            if (_connectedUsers.TryGetValue(Context.ConnectionId, out var info))
            {
                info.Status = status;
                await BroadcastUserList();
            }
        }
    }
}
