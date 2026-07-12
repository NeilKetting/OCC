using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;
using OCC.Shared.DTOs;

namespace OCC.WpfClient.Services.Interfaces
{
    public interface IAuditLogService
    {
        Task<AuditLogsResponseDto?> GetAuditLogsAsync(string? search, Guid? userId, DateTime? startDate, DateTime? endDate, int skip, int take);
        Task<int> GetTotalCountAsync();
    }
}
