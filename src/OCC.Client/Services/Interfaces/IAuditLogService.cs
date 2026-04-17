using System.Collections.Generic;
using System.Threading.Tasks;
using OCC.Shared.Models;

namespace OCC.Client.Services.Interfaces
{
    public interface IAuditLogService
    {
        Task<IEnumerable<AuditLog>> GetAuditLogsAsync();
    }
}
