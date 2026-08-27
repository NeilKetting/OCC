using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.DTOs;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface ISignalRService
    {
        event Action<List<UserConnectionInfo>> UserListUpdated;
        event Action<string> NotificationReceived;
        event Action<DashboardUpdateDto> DashboardUpdateReceived;
        event Action<ChatMessageDto>? ChatMessageReceived;
        event Action<Guid>? SessionDeleted;

        // Time & Attendance Delta Payload Real-Time Streaming Events
        event Action<EntityChangeDto<OCC.Shared.Models.Employee>>? OnEmployeeChanged;
        event Action<EntityChangeDto<OCC.Shared.Models.AttendanceRecord>>? OnAttendanceRecordChanged;
        event Action<EntityChangeDto<OCC.Shared.Models.WageRun>>? OnWageRunChanged;
        event Action<EntityChangeDto<OCC.Shared.Models.WageSettings>>? OnWageSettingsChanged;

        // Expanded Delta Payload Streaming Events
        event Action<EntityChangeDto<ProjectSummaryDto>>? OnProjectChanged;
        event Action<EntityChangeDto<SupplierSummaryDto>>? OnSupplierChanged;
        event Action<EntityChangeDto<CustomerSummaryDto>>? OnCustomerChanged;
        event Action<EntityChangeDto<SubContractorSummaryDto>>? OnSubContractorChanged;
        event Action<EntityChangeDto<OCC.Shared.Models.SnagJob>>? OnSnagJobChanged;
        event Action<EntityChangeDto<IncidentSummaryDto>>? OnIncidentChanged;
        event Action<EntityChangeDto<AuditSummaryDto>>? OnAuditChanged;
        event Action<EntityChangeDto<HseqTrainingSummaryDto>>? OnTrainingChanged;

        
        bool IsConnected { get; }
        bool IsChatConnected { get; }
        int OnlineCount { get; }
        List<UserConnectionInfo> OnlineUsers { get; }
        
        Task StartAsync();
        Task StopAsync();
        Task RestartAsync();
        Task UpdateStatusAsync(string status);
        
        Task SendChatMessageAsync(Guid sessionId, string content);
        Task MarkChatSessionAsReadAsync(Guid sessionId);
        Task<bool> ToggleChatFavouriteAsync(Guid sessionId);
    }
}
